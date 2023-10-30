-- Procedure: create_authorizationevent
CREATE OR REPLACE FUNCTION authz.create_authorizationevent(
	_sessionid text,
	_created timestamp,
	_subjectuserid INTEGER,
	_subjectorgcode text,
	_subjectorgnumber INTEGER,
	_subjectparty INTEGER,
	_resourcepartyid INTEGER,
	_resource text,
	_instanceid text,
	_operation text,
	_ipaddress text,
	_contextrequestjson jsonb,
	_decision integer)
    RETURNS authz.eventlog
    LANGUAGE 'sql'
    COST 100
    VOLATILE PARALLEL SAFE 
AS $BODY$
INSERT INTO authz.eventlog(
	sessionid,
	created,
	subjectuserid,
	subjectorgcode,
	subjectorgnumber,
	subjectparty,
	resourcepartyid,
	resource,
	instanceid,
	operation,
	ipaddress,
	contextrequestjson,
	decision
)
VALUES (
	_sessionid,
	_created,
	_subjectuserid,
	_subjectorgcode,
	_subjectorgnumber,
	_subjectparty,
	_resourcepartyid,
	_resource,
	_instanceid,
	_operation,
	_ipaddress,
	_contextrequestjson,
	_decision
)
RETURNING *;
$BODY$;