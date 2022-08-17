declare
    v_store1_start date;
    v_store1_end date;
    v_store2_start date; -- buf_store2_card, comp_store2_card, hist_store2_card
    v_store2_end date;
    
begin
    v_store1_start := to_date('20140101','YYYYMMDD'); -- 20150401
    v_store1_end := to_date('20140401', 'YYYYMMDD'); -- 20150701
    v_store2_start := to_date('20140101', 'YYYYMMDD');
    v_store2_end := to_date('20140401', 'YYYYMMDD');


begin
execute immediate 'drop table temp_cp_store1_store2';
exception 
  when others then null;
end;

templog_pack.add_message(1,'Starting gathering store1_store2_cp','debug');

execute immediate 'create table temp_cp_store1_store2 as 
select b.id, b.code1, b.code2, b.full_name
     , f.person_id, b.filename
     , store1.code1 as store1_code1, store1.code2 as store1_code2, store1.code3 as store1_code3
     , store1.person_id as store1_person_id, store1.dtactual as store1_date, store1.fullname as store1_name
     , 1 as reason 
  from buf_store2_card b
inner join buf_object_link l on l.object_id = b.id and l.code = ''STORE2CARD''
inner join comp_store2_card f on f.id = l.comp2_object_id
inner join (select i.code1, i.code2, u.code3, u.person_id, u.dtactual, n.fullname from hist_store1_ent_code1 i 
                                     inner join hist_store1_ent u on u.id = i.ent_id
                                     inner join hist_store1_ent_name n on n.ent_id = u.id ) store1
         on store1.code1 = b.code1 
where b.doc_date between to_date('||to_char(v_store2_start,'YYYYMMDD')||',''YYYYMMDD'') and to_date('||to_char(v_store2_end,'YYYYMMDD')||',''YYYYMMDD'')
and b.is_ent = 1
and f.person_id <> store1.person_id'; -- 165

templog_pack.add_message(1,'Gathering store1_store2_cp, stage 1 complete','debug');

execute immediate '
insert into temp_cp_store1_store2
select b.id, b.code1, b.code2, b.full_name,  f.person_id, b.filename, store1.*, 2 as reason from buf_store2_card b
inner join buf_object_link l on l.object_id = b.id and l.code = ''STORE2CARD''
inner join comp_store2_card f on f.id = l.comp2_object_id
inner join (select i.code1, i.code2, u.code3, u.person_id, u.dtactual, n.fullname from hist_store1_ent_code1 i 
                                     inner join hist_store1_ent u on u.id = i.ent_id
                                     inner join hist_store1_ent_name n on n.ent_id = u.id ) store1
         on store1.code1 = b.code1 
where store1.dtactual between :1 and :2
and b.is_ent = 1
and f.person_id <> store1.person_id' using v_store1_start, v_store1_end; 

templog_pack.add_message(1,'Gathering store1_store2_cp, complete','debug');

commit;               

end;