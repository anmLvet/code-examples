declare
    procedure SaveSetParam(pName varchar2, pValue varchar2, pDescription varchar2) is
    begin
         update sysparameters
            set paramvalue = pValue
          where paramname = pName;

         if (sql%notfound) then
             insert into sysparameters(paramname, paramvalue, description)
             values (pName, pValue, pDescription);
         end if;
    end;
begin
    SaveSetParam('MQInit',';MC1;MK1;','Opening messages, semicolon-delimited code list');

end;
/

commit;

COMMENT ON COLUMN MQ_TRANSITION.MESSAGE_ID IS 'Message type id';

alter table mq_in_messages
add (receiver_exchtype varchar2(8)
    ,sender_dep varchar2(8)
    ,sender_exchtype varchar2(8)
    );

comment on column mq_in_messages.receiver_exchtype is 'Receiver exchange state code';
comment on column mq_in_messages.sender_dept is 'Sender dept code';
comment on column mq_in_messages.sender_exchtype is 'Sender exchange state code';

insert into mq_state (id,code,name,description) values (18,'Inform','Response for inforequest 1','');

insert into mq_doc_type (id,code, name, xml_name, format_type) values (23, '-', 'Inforequest 1', 'Target1CommonReq', '7.0');

insert into mq_msg_type (id,code,name,direction,mq_doc_type_id,pack_allowed, message_class) values (30,'MK1','Inforequest 2',1,23,0,'Inf');
insert into mq_msg_type (id,code,name,direction,mq_doc_type_id,pack_allowed, message_class) values (31,'MK8','Response for inforequest 2',2,4,1,'Inf');
insert into mq_msg_type (id,code,name,direction,mq_doc_type_id,pack_allowed, message_class) values (32,'MK9','No info for inforequest 2',2,22,0,'Cmn');

insert into mq_pack_message(id,message_id,document_id,is_main,date_start) values (16,31,4,1,sysdate);
insert into mq_pack_message(id,message_id,document_id,is_main,date_start) values (17,31,4,0,sysdate);

insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)  values (101,30,null,18,sysdate); 
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)  values (102,31,18,18,sysdate);
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)  values (103,32,18,18,sysdate);

commit;

-- info modification by dept
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)  values (111,14,7,7,sysdate); -- MC11 
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)  values (112,14,10,10,sysdate);

commit;

---- update 11.11.2015 ---------------------

insert into mq_state (id,code,name,description) values (0,'Start','Interaction start','');
update mq_transition set to_state_id = 0 where id = 21; 
-- state change after init request
update mq_transition set from_state_id = 0 where id = 1; 
-- state change after reply on init request
-- new states
insert into mq_state (id,code,name,description) values (21,'OP1Finish','Request 1 is ok','');
insert into mq_state (id,code,name,description) values (19,'OP2Finish','Request 2 is ok','');
insert into mq_state (id,code,name,description) values (20,'OP3Finish','Request 3 is ok','');

-- new doc_types
insert into mq_doc_type (id,code, name, xml_name, format_type) values (24, 'MK5', 'Request 4 is ok', 'OP4Finish', '7.0');
insert into mq_doc_type (id,code, name, xml_name, format_type) values (25, 'MK4', 'Additional document list', 'AdditionalInventory', '7.0');

-- new msg_types
insert into mq_msg_type (id,code,name,direction,mq_doc_type_id,pack_allowed, message_class) values (33,'MC2','Request 5 is ok',2,24,0,'Notif');
insert into mq_msg_type (id,code,name,direction,mq_doc_type_id,pack_allowed, message_class) values (34,'MC3','Request extra info for request 2',2,18,0,'ReqAddDoc');
insert into mq_msg_type (id,code,name,direction,mq_doc_type_id,pack_allowed, message_class) values (35,'MC4','Additional documents list',1,25,0,'AddDoc');

-- new msg_packs
insert into mq_pack_message(id,message_id,document_id,is_main,date_start) values (18,28,4,1,sysdate);
insert into mq_pack_message(id,message_id,document_id,is_main,date_start) values (19,28,4,0,sysdate);
-- fix pack msg type
update mq_msg_type set mq_doc_type_id = 1 where code in ('MC6','MC8','MC7','MK1');

commit;
--- state change after info change
delete from mq_transition where from_state_id in (14,15,16,17) or to_state_id in (14,15,16,17); 
-- removing previous variant

-- info change after request was checked
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (61,18,7,14,sysdate);                                                      
  -- MC6
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (62,9,14,19,sysdate);                                                      
  -- MC10
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (63,28,15,7,sysdate);                                                      
  -- MC7
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (64,33,19,15,sysdate);                                                     
  -- MC2
-- info change on stage 1
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start) 
  values (65,18,10,16,sysdate);                                                     
  -- MC6
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (66,9,16,20,sysdate);                                                      
  -- MC10
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (67,28,17,10,sysdate);                                                     
  -- MC7
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (68,33,20,17,sysdate);                                                     
  -- MC2

update mq_transition set from_state_id = 21 where id in (11,9,51); 
-- MC11..22 - should be sent only after request check
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (121,33,4,21,sysdate);                                                     
  -- MC2

update mq_doc_type set code = 'MK3' where id = 23; 
-- doc type fix for request 3

commit;

declare
    procedure SaveSetParam(pName varchar2, pValue varchar2, pDescription varchar2) is
    begin
         update sysparameters
            set paramvalue = pValue
          where paramname = pName;

         if (sql%notfound) then
             insert into sysparameters(paramname, paramvalue, description)
             values (pName, pValue, pDescription);
         end if;
    end;
begin
    SaveSetParam('MQSignOutgoing','1','Signing outgoing messages required');
    SaveSetParam('MQLinkPack',';MC9;MC5;MC10;MC11;MC12;MC13;MC7;MC14;MC15;MC16;','Messages, for which pack continuty required, semicolon-delimited list');
    SaveSetParam('MQLinkDocument',';MC9;MC5;MC10;MC11;MC12;MC13;MC7;MC14;MC15;MC16;','Messages, for which document-level continuty required, semicolon-delimited list');

end;
/

commit;
    
alter table mq_outgoing
add (softversion varchar2(50));

comment on column mq_outgoing.softversion is 'softversion field value';    

-- Extra info requests
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (131,34,4,4,sysdate);     
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (132,35,4,4,sysdate);     
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (133,34,21,21,sysdate);     
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (134,35,21,21,sysdate);     
-- Transfer to other dept
insert into mq_transition (id, message_id, from_state_id, to_state_id, date_start)
  values (135,11,21,21,sysdate);           

commit;  