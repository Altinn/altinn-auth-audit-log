
-- Table: authentication.eventlog
CREATE TABLE IF NOT EXISTS authentication.eventlogv1
(
	sessionid text,
	externalsessionid text,
	subscriptionkey text,
	externaltokenissuer text,
	created timestamp with time zone NOT NULL,
	userid integer,
	supplierid text,
	orgnumber integer,
	eventtypeid integer,
	authenticationmethodid integer,
	authenticationlevelid integer,
	ipaddress text,
	isauthenticated boolean NOT NULL,
	CONSTRAINT authenticationeventtype_fkey FOREIGN KEY (eventtypeid)
        REFERENCES authentication.authenticationeventtype (authenticationeventtypeid) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    CONSTRAINT authenticationlevel_fkey FOREIGN KEY (authenticationlevelid)
        REFERENCES authentication.authenticationlevel (authenticationlevelid) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    CONSTRAINT authenticationmethod_fkey FOREIGN KEY (authenticationmethodid)
        REFERENCES authentication.authenticationmethod (authenticationmethodid) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
) PARTITION BY RANGE (created);

CREATE INDEX ON authentication.eventlogv1 (created);

-- Table: authz.eventlog
CREATE TABLE IF NOT EXISTS authz.eventlogv1
(
	sessionid text,
	created timestamp with time zone NOT NULL,
	subjectuserid INTEGER,
	subjectorgcode text,
	subjectorgnumber INTEGER,
	subjectparty INTEGER,
	resourcepartyid INTEGER,
	resource text,
	instanceid text,
	operation text,
	ipaddress text,
	contextrequestjson jsonb,
	decision integer,
	CONSTRAINT authzdecisionid_fkey FOREIGN KEY (decision)
        REFERENCES authz.decision (decisionid) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
) PARTITION BY RANGE (created);

CREATE INDEX ON authz.eventlogv1 (created);
