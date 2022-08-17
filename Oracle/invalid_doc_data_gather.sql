declare
   vDocumentID varchar2(100);
   vMsg1 varchar2(40);
   vMsg2 varchar2(2);
   vMsg3 varchar2(40);
   vDecl varchar2(20);
   vMsg4 varchar2(10);

   vInvDoc number(6);
   vInvDocInfo varchar2(2000);
   vRows number(6);

   function TagClobValue (pSource in clob, pTag in varchar2, pOccurence in number) return varchar2
   is 
       vOuterTag varchar2(2000);
   begin
       vOuterTag := regexp_substr(pSource,'<'||pTag||'[>\s].*?<\/'||pTag||'>',1,pOccurrence,'cn');
       if (vOuterTag is not null) then
            vOuterTag := substr (vOuterTag, instr(vOuterTag,'>')+1);
            vOuterTag := substr (vOuterTag, 1, instr(vOuterTag,'<\/'||pTag)-1);
       else
           return null;
       end if;
       return vOuterTag;      
   end;

   function TagValue (pSource in varchar2, pTag in varchar2, pOccurence in number) return varchar2
   is 
       vOuterTag varchar2(2000);
   begin
       vOuterTag := regexp_substr(pSource,'<'||pTag||'[>\s].*?<\/'||pTag||'>',1,pOccurrence,'cn');
       if (vOuterTag is not null) then
            vOuterTag := substr (vOuterTag, instr(vOuterTag,'>')+1);
            vOuterTag := substr (vOuterTag, 1, instr(vOuterTag,'<\/'||pTag)-1);
       else
           return null;
       end if;
       return vOuterTag;      
   end;
begin
   for rec in (select messagetext from mq_buf_raw)
   loop
       vDocumentID := TagClobValue(rec.messagetext, 'dict:DocumentID', 1);
       vDecl := TagClobValue(rec.messagetext, 'contr:Code1',1);
       vMsg2 := TagClobValue(rec.messagetext, 'contr:Code2',1);
       if (vDocumentID is not null) then
            vInvDoc := 1;
            while true 
            loop
                vInvDocInfo := TagClobValue(rec.messagetext, 'contr:Info', vInvDoc);
                exit when vInvDocInfo is null;

                vMsg4 := TagValue(vInvDocInfo,'contr:ItemID',1);
                vMsg1 := TagValue(vInvDocInfo,'contr:Code3',1);
                vMsg3 := TagValue(vInvDocInfo,'contr:Weight',1);

                update InvalidDoc
                   set Msg1 = vMsg1, Msg3 = vMsg3, Decl = vDecl, Msg2 = vMsg2
                 where InvalidDocID like 'vDocumentID.%'
                   and Msg4 = vMsg4
                   and createuserid = 'MQ_EX';

                 vRows := sql%rowcount;
 
                 dbms_output.put_line ('for doc '||vDocumentID||', item '||vMsg4||' updated '||vRows||' rows');

                vInvDoc := vInvDoc + 1;
            end loop;
       end if;    
   endloop;
end;