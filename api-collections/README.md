# DigitalTwin2 API Collections

Pre-configured API testing collections for **DigitalTwin2** - comprehensive test suites for all endpoints with automated validation.

## 📁 Collection Types

### **Bruno Collections** (Recommended)
Modern, lightweight API testing with file-based collections:

```
api-collections/bruno/DigitalTwin2-API/
├── meta/bruno.json                    # Collection metadata  
├── environments/Local.bru             # Local development environment
├── Health/API Health.bru              # Health check endpoint
├── Knowledge Graph/
│   ├── Create Entity.bru              # Create Knowledge Graph entity
│   └── Get Entity.bru                 # Retrieve entity by ID
├── Document Ingestion/
│   ├── Upload Document.bru            # Single document processing
│   └── Get Processing Status.bru      # Check processing progress
└── Cold Start/
    ├── Batch Upload Documents.bru     # Bulk document processing
    └── Get Knowledge Graph Stats.bru  # Verify cold start success
```

### **Postman Collections** (Classic)  
Traditional Postman format with full feature support:

```
api-collections/postman/
├── DigitalTwin2-API.postman_collection.json     # Complete collection
└── DigitalTwin2-Local.postman_environment.json  # Local environment
```

## 🚀 Quick Start

### Using Bruno (Recommended)
1. **Install Bruno**: `npm install -g @usebruno/cli`
2. **Open Collection**: `bruno open api-collections/bruno/DigitalTwin2-API`
3. **Set Environment**: Select "Local" environment
4. **Configure Auth**: Set `authToken` in environment variables
5. **Run Tests**: Execute individual requests or full collection

### Using Postman
1. **Import Collection**: File → Import → `DigitalTwin2-API.postman_collection.json`
2. **Import Environment**: Import → `DigitalTwin2-Local.postman_environment.json`
3. **Select Environment**: Choose "DigitalTwin2 Local" from dropdown
4. **Set Auth Token**: Update `authToken` environment variable
5. **Run Tests**: Use Collection Runner for full test suite

## 🔧 Configuration

### Environment Variables
```javascript
// Required for all requests
baseUrl: "http://localhost:5225"          // API base URL
apiVersion: "v1"                          // API version
dataRoomId: "test-dataroom-01"           // Data room for testing
authToken: "your-jwt-token-here"         // Bearer authentication

// Auto-populated by tests
correlationId: "correlation-1703123456"   // Request correlation
entityId: "ENT_01HF7K8..."              // Created entity ID
processingId: "PROC_01HF7K8..."         // Processing job ID
```

### Authentication Setup
```bash
# Get JWT token (development)
curl -X POST http://localhost:5225/api/v1/auth/login \\
  -H "Content-Type: application/json" \\
  -d '{"username": "dev@digitaltwin2.com", "password": "dev-password"}'

# Extract token and set in environment
export AUTH_TOKEN="eyJhbGciOiJIUzI1NiIs..."
```

## 📋 Test Scenarios

### **Health Check Flow**
1. ✅ API health verification
2. ✅ Response time validation (<1000ms)
3. ✅ Service status confirmation

### **Knowledge Graph Flow**
1. ✅ Create vendor entity with attributes
2. ✅ Verify entity creation and ID assignment  
3. ✅ Retrieve entity and validate data integrity
4. ✅ Test attribute confidence scoring

### **Document Processing Flow**
1. ✅ Upload single document (PDF/CSV/XLSX)
2. ✅ Verify asynchronous processing initiation
3. ✅ Poll processing status until completion
4. ✅ Validate extraction results and entity creation

### **Cold Start Flow**
1. ✅ Batch upload document archive
2. ✅ Monitor batch processing progress
3. ✅ Verify Knowledge Graph population
4. ✅ Validate cold start success criteria:
   - Total entities > 100
   - Evidence coverage > 90%
   - Processing success rate > 95%

## 🎯 Testing Patterns

### Request Correlation
Every request includes correlation headers for audit trails:
```javascript
// Auto-generated correlation ID
"X-Correlation-ID": "correlation-{{$timestamp}}"
"X-DataRoom-ID": "{{dataRoomId}}"
```

### Variable Chaining
Test results populate variables for subsequent requests:
```javascript
// Create entity test populates entityId
pm.globals.set('entityId', jsonData.id);

// Get entity test uses populated entityId
/api/v1/knowledge-graph/entities/{{entityId}}
```

### Confidence Validation
Document processing tests validate confidence scoring:
```javascript
pm.test('Extraction confidence acceptable', function() {
    const jsonData = pm.response.json();
    pm.expect(jsonData.overallConfidence).to.be.above(0.6);
});
```

### Evidence Traceability  
Verify evidence links for all extracted facts:
```javascript
pm.test('Facts have evidence links', function() {
    const facts = pm.response.json().facts;
    facts.forEach(fact => {
        pm.expect(fact.evidenceId).to.be.a('string');
        pm.expect(fact.confidence).to.be.above(0.5);
    });
});
```

## 📊 Performance Benchmarks

### Target Response Times
- **Health Check**: <100ms
- **Entity Creation**: <500ms
- **Document Upload**: <1000ms  
- **Batch Processing**: <5000ms initialization

### Throughput Targets
- **Single Document**: 10-20 documents/minute
- **Batch Processing**: 100+ documents/hour
- **Cold Start**: 2000+ documents in <4 hours

## 🛠️ Development Workflow

### Pre-commit Testing
```bash
# Run health checks before committing
bruno run api-collections/bruno/DigitalTwin2-API/Health

# Validate Knowledge Graph operations
bruno run api-collections/bruno/DigitalTwin2-API/Knowledge\\ Graph
```

### CI/CD Integration
```yaml
# GitHub Actions example
- name: Test DigitalTwin2 APIs
  run: |
    bruno run api-collections/bruno/DigitalTwin2-API \\
      --env Local \\
      --output junit.xml
```

### Load Testing
```bash
# Scale testing with multiple data rooms
for i in {1..10}; do
  export DATA_ROOM_ID="load-test-$i"
  bruno run api-collections/bruno/DigitalTwin2-API/Cold\\ Start
done
```

## 🚨 Troubleshooting

### Common Issues
**401 Unauthorized**: Update `authToken` environment variable
**403 Forbidden**: Verify `dataRoomId` permissions
**422 Validation Error**: Check request payload format
**500 Server Error**: Verify API service health

### Debug Mode
Enable detailed logging in Bruno/Postman:
```javascript
// Add to test scripts for debugging
console.log('Request:', pm.request);
console.log('Response:', pm.response.json());
console.log('Variables:', pm.variables.toObject());
```

The API collections provide comprehensive testing coverage for the entire DigitalTwin2 system - from individual entity operations to complete cold start procedures with full validation and performance benchmarking.