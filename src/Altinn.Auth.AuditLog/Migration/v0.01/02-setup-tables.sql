-- Table: authz.eventlog
CREATE TABLE IF NOT EXISTS authz.eventlog
(
	identifier bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	created timestamp with time zone NOT NULL,
	subjectuserid text,
	subjectparty text,
	resourcepartyid text,
	resource text,
	instanceid text,
	operation text,
	TimeToDelete text,
	IpAdress text,
	contextrequestjson jsonb
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authz.eventlog TO auth_auditlog;

GRANT ALL ON TABLE authz.eventlog TO auth_auditlog_admin;
