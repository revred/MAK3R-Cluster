PRAGMA journal_mode=WAL; 
PRAGMA synchronous=NORMAL; 
PRAGMA temp_store=MEMORY;

CREATE TABLE IF NOT EXISTS net_session (
  session_id     TEXT PRIMARY KEY,
  started_at_utc TEXT NOT NULL,
  site_id        TEXT NOT NULL,
  edge_version   TEXT NOT NULL,
  hub_url        TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS net_phase (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id     TEXT NOT NULL,
  ts_utc         TEXT NOT NULL,
  phase          TEXT NOT NULL,
  latency_ms     INTEGER,
  ok             INTEGER NOT NULL,
  error_code     TEXT,
  error_detail   TEXT
);
CREATE INDEX IF NOT EXISTS ix_net_phase_session_ts ON net_phase(session_id, ts_utc);

CREATE TABLE IF NOT EXISTS uplink_batch (
  batch_id       TEXT PRIMARY KEY,
  enq_ts_utc     TEXT NOT NULL,
  send_ts_utc    TEXT,
  ack_ts_utc     TEXT,
  events_count   INTEGER NOT NULL,
  bytes          INTEGER NOT NULL,
  ack_ok         INTEGER,
  ack_err        TEXT
);

CREATE TABLE IF NOT EXISTS queue_sample (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  ts_utc         TEXT NOT NULL,
  depth          INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS event_stats (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  ts_utc         TEXT NOT NULL,
  produced       INTEGER NOT NULL,
  sent           INTEGER NOT NULL,
  acked          INTEGER NOT NULL,
  deduped        INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS event_audit (
  event_id       TEXT PRIMARY KEY,
  ts_utc         TEXT NOT NULL,
  machine_id     TEXT NOT NULL,
  enq_ts_utc     TEXT NOT NULL,
  send_ts_utc    TEXT,
  ack_ts_utc     TEXT,
  state_exec     TEXT
);
CREATE INDEX IF NOT EXISTS ix_event_audit_ts ON event_audit(ts_utc);
