SELECT create_hypertable('authz.eventlog','created', chunk_time_interval => INTERVAL '1 year', migrate_data => true);