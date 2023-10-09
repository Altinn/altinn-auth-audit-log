-- Procedure: create_authorizationevent
CREATE OR REPLACE FUNCTION authz.create_authorizationevent(
	_created timestamp,
	_subjectuserid INTEGER,
	_subjectorgcode text,
	_subjectorgnumber INTEGER,
	_subjectparty INTEGER,
	_resourcepartyid INTEGER,
	_resource text,
	_instanceid text,
	_operation text,
	_timetodelete timestamp,
	_ipaddress text,
	_contextrequestjson jsonb,
	_decision text)
    RETURNS authz.eventlog
    LANGUAGE 'sql'
    COST 100
    VOLATILE PARALLEL SAFE 
AS $BODY$
INSERT INTO authz.eventlog(
	created,
	subjectuserid,
	subjectorgcode,
	subjectorgnumber,
	subjectparty,
	resourcepartyid,
	resource,
	instanceid,
	operation,
	timetodelete,
	ipaddress,
	contextrequestjson,
	decision
)
VALUES (
	_created,
	_subjectuserid,
	_subjectorgcode,
	_subjectorgnumber,
	_subjectparty,
	_resourcepartyid,
	_resource,
	_instanceid,
	_operation,
	_timetodelete,
	_ipaddress,
	_contextrequestjson,
	_decision
)
RETURNING *;
$BODY$;