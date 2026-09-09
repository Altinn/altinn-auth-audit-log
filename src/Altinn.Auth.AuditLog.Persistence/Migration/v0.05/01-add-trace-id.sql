-- Add field trace_id
ALTER TABLE authz.eventlogv1
ADD trace_id VARCHAR(63) NULL;

-- Add index for trace id
CREATE INDEX authz_eventlogv1_trace_id_idx ON authz.eventlogv1 (trace_id);
