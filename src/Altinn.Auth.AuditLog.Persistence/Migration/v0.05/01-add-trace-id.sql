-- Add field trace_id
ALTER TABLE authz.eventlogv1
ADD trace_id VARCHAR(63) NULL;
