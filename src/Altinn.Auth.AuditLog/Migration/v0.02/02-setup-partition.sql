CREATE TABLE authentication.eventlogv1_y2024m10 PARTITION OF authentication.eventlogv1
    FOR VALUES FROM ('2024-10-01') TO ('2024-11-01');

CREATE TABLE authentication.eventlogv1_y2024m11 PARTITION OF authentication.eventlogv1
    FOR VALUES FROM ('2024-11-01') TO ('2024-12-01');

CREATE TABLE authentication.eventlogv1_y2024m12 PARTITION OF authentication.eventlogv1
    FOR VALUES FROM ('2024-12-01') TO ('2025-01-01');

CREATE TABLE authz.eventlogv1_y2024m10 PARTITION OF authz.eventlogv1
    FOR VALUES FROM ('2024-10-01') TO ('2024-11-01');

CREATE TABLE authz.eventlogv1_y2024m11 PARTITION OF authz.eventlogv1
    FOR VALUES FROM ('2024-11-01') TO ('2024-12-01');

CREATE TABLE authz.eventlogv1_y2024m12 PARTITION OF authz.eventlogv1
    FOR VALUES FROM ('2024-12-01') TO ('2025-01-01');
