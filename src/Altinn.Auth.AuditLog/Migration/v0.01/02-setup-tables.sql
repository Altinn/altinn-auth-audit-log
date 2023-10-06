-- Table: authz.eventlog
CREATE TABLE IF NOT EXISTS authz.eventlog
(
	identifier bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	created timestamp with time zone NOT NULL,
	subjectuserid INTEGER,
	subjectorgcode text,
	subjectorgnumber INTEGER,
	subjectparty INTEGER,
	resourcepartyid INTEGER,
	resource text,
	instanceid text,
	operation text,
	timetodelete timestamp with time zone NOT NULL,
	ipaddress text,
	contextrequestjson jsonb,
	decision text
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authz.eventlog TO auth_auditlog;

GRANT ALL ON TABLE authz.eventlog TO auth_auditlog_admin;
