-- Table: authz.eventlog
CREATE TABLE IF NOT EXISTS authz.eventlog
(
	identifier bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	created timestamp with time zone NOT NULL,
	subjectuserid text,
	subjectorgcode text,
	subjectorgnumber text,
	subjectparty text,
	resourcepartyid text,
	resource text,
	instanceid text,
	operation text,
	timetodelete text,
	ipadress text,
	contextrequestjson jsonb,
	decision text
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authz.eventlog TO auth_auditlog;

GRANT ALL ON TABLE authz.eventlog TO auth_auditlog_admin;
