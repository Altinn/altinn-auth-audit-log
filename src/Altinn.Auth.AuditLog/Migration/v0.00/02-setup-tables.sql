-- Table: authentication.eventlog
CREATE TABLE IF NOT EXISTS authentication.eventlog
(
	identifier bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	created timestamp with time zone NOT NULL,
	userid integer,
	supplierid text,
	eventtype text,
	orgnumber integer,
	authenticationmethod text,
	authenticationlevel text,
	ipaddress text,
	isauthenticated boolean NOT NULL,
	timetodelete timestamp with time zone NOT NULL
)
TABLESPACE pg_default;

GRANT ALL ON TABLE authentication.eventlog TO auth_auditlog;

GRANT ALL ON TABLE authentication.eventlog TO auth_auditlog_admin;
