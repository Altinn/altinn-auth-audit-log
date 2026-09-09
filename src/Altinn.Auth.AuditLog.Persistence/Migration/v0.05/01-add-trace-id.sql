-- Add new authentication methods
ALTER TABLE authz.eventlogv1
ADD trace_id VARCHAR(255);
