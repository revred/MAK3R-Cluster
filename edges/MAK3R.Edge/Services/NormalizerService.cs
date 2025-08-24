using Mak3r.Edge.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Mak3r.Edge.Services;

public class NormalizerService : BackgroundService
{
    private readonly QueueService _queue;
    private readonly ILogger<NormalizerService> _log;
    private readonly EdgeConfig _config;
    private readonly ConcurrentDictionary<string, DateTime> _lastEventTimes = new();
    private readonly ConcurrentDictionary<string, string> _lastStates = new();

    public NormalizerService(QueueService queue, IOptions<EdgeConfig> config, ILogger<NormalizerService> log)
    {
        _queue = queue; 
        _config = config.Value;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("NormalizerService active - ready to process events from real connectors");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Normalizes and enriches machine events before queuing for uplink
    /// </summary>
    public async Task EnqueueAsync(KMachineEvent e, CancellationToken ct = default)
    {
        try
        {
            // Apply normalization rules
            var normalizedEvent = await ApplyNormalizationRules(e, ct);
            if (normalizedEvent == null) return;

            // Apply business logic and state tracking
            var enrichedEvent = ApplyBusinessLogic(normalizedEvent);
            if (enrichedEvent == null) return;

            // Queue for uplink
            await _queue.WriteAsync(enrichedEvent, ct);
            _log.LogDebug("Normalized and queued event from {MachineId}: {EventType}", 
                enrichedEvent.MachineId, enrichedEvent.Event?.Type);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error normalizing event from machine {MachineId}", e.MachineId);
        }
    }

    private async Task<KMachineEvent?> ApplyNormalizationRules(KMachineEvent evt, CancellationToken ct)
    {
        // Ensure required fields are populated
        if (string.IsNullOrEmpty(evt.SiteId))
            evt.SiteId = _config.SiteId;

        if (string.IsNullOrEmpty(evt.MachineId))
        {
            _log.LogWarning("Event missing MachineId - dropping");
            return null;
        }

        // Ensure timestamp is UTC
        if (evt.Ts.Kind != DateTimeKind.Utc)
            evt.Ts = evt.Ts.ToUniversalTime();

        // Set default availability if not specified
        evt.State ??= new StateInfo();
        if (string.IsNullOrEmpty(evt.State.Availability))
            evt.State.Availability = "AVAILABLE";

        return evt;
    }

    private KMachineEvent? ApplyBusinessLogic(KMachineEvent evt)
    {
        var machineKey = evt.MachineId;
        
        // Track state transitions for event generation
        var currentExecution = evt.State?.Execution;
        var lastExecution = _lastStates.GetValueOrDefault($"{machineKey}_execution");
        
        if (!string.IsNullOrEmpty(currentExecution) && currentExecution != lastExecution)
        {
            _lastStates[$"{machineKey}_execution"] = currentExecution;
            
            // Generate state transition events
            evt.Event ??= new EventInfo();
            if (string.IsNullOrEmpty(evt.Event.Type))
            {
                evt.Event.Type = InferEventTypeFromTransition(lastExecution, currentExecution);
            }
        }

        // Add cycle time calculation for part completion events
        if (evt.Event?.Type == "PART_COMPLETED")
        {
            AddCycleTimeCalculation(evt, machineKey);
        }

        // Filter out duplicate events (debouncing)
        if (IsDuplicateEvent(evt, machineKey))
        {
            _log.LogDebug("Filtering duplicate event from {MachineId}", evt.MachineId);
            return null;
        }

        return evt;
    }

    private string? InferEventTypeFromTransition(string? from, string? to)
    {
        return (from, to) switch
        {
            (_, "ACTIVE") => "CYCLE_START",
            ("ACTIVE", "READY") => "CYCLE_STOP", 
            ("ACTIVE", "INTERRUPTED") => "FEED_HOLD",
            ("INTERRUPTED", "ACTIVE") => "FEED_RESUME",
            (_, "ALARM") => "ALARM",
            _ => null
        };
    }

    private void AddCycleTimeCalculation(KMachineEvent evt, string machineKey)
    {
        var lastCycleStartKey = $"{machineKey}_cycle_start";
        if (_lastEventTimes.TryGetValue(lastCycleStartKey, out var lastCycleStart))
        {
            var cycleTimeMs = (int)(evt.Ts - lastCycleStart).TotalMilliseconds;
            evt.Context ??= new ContextInfo();
            evt.Context.Job ??= new JobInfo();
            
            // Add cycle time to event context (could be extended to job context)
            _log.LogInformation("Cycle completed for {MachineId} in {CycleTimeMs}ms", evt.MachineId, cycleTimeMs);
        }
    }

    private bool IsDuplicateEvent(KMachineEvent evt, string machineKey)
    {
        var eventKey = $"{machineKey}_{evt.Event?.Type}_{evt.Event?.Code}";
        var now = evt.Ts;
        
        if (_lastEventTimes.TryGetValue(eventKey, out var lastTime))
        {
            // Filter events within 5 seconds of each other
            if ((now - lastTime).TotalSeconds < 5)
                return true;
        }
        
        _lastEventTimes[eventKey] = now;
        return false;
    }
}
