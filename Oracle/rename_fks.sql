declare 
   
   procedure analyze_fk(fk_name varchar2, new_name varchar2, fk_column varchar2) is
       v_table_name varchar2(50);
       v_type varchar2(10);
       v_same varchar2(60);
       v_r_table varchar2(60);
   begin
       select u.table_name, u.constraint_type, r.table_name
         into v_table_name, v_type, v_r_table
         from user_constraints u
  left join user_constraints r on U.R_CONSTRAINT_NAME = R.CONSTRAINT_NAME
        where u.constraint_name = fk_name;
        
        if (v_r_table is null) then v_r_table := '-'; end if;
      
      dbms_output.put_line('renamefk('''||v_table_name||''','''||v_r_table||''','''||fk_column||''','''||new_name||'''); -- '||fk_name);

      -- code below only for self-check, that correct relation was found
       dbms_output.put_line('--------------');
       for rec in (select u.*, R.TABLE_NAME r_table, CC.COLUMN_NAME cons_col from user_constraints u
                left join user_constraints r on U.R_CONSTRAINT_NAME = R.CONSTRAINT_NAME
                left join USER_CONS_COLUMNS cc on CC.CONSTRAINT_NAME = U.CONSTRAINT_NAME
                    where u.table_name = v_Table_name
                      and u.constraint_Type = v_type
                      and nvl(R.TABLE_NAME,'-') = v_r_table
                      and ( fk_column is null or fk_column = cc.column_name)
                  )
       loop
           v_same := '';
           if (rec.constraint_name = fk_name) then
                v_same := ' <-- *** this constraint';
           end if;
           
           --dbms_output.put_line(rec.table_name||' '||rec.constraint_name||' '||rec.constraint_type||' search='||rec.search_condition||' external_table='||rec.r_table||' cons_col='||rec.cons_col||v_same);
       end loop;
   end;
   
      procedure analyzefk(fk_name varchar2, new_name varchar2) is
   begin
       analyze_fk(fk_name, new_name, null);
   end;

begin
  analyze_fk('SYS_id1','comp_ent1_ent2', 'check_column');
  analyzefk('SYS_id2','comp_ent3_ent4');
  -- ~ 40 lines skipped

end;

declare 
    procedure rename_fk(p_parent varchar2, p_relative varchar2, p_column varchar2, p_new_name varchar2, p_type varchar2) is 
    begin
        for rec in (select u.constraint_name from user_constraints u
                   left join user_constraints r on U.R_CONSTRAINT_NAME = R.CONSTRAINT_NAME
                   left join USER_CONS_COLUMNS cc on CC.CONSTRAINT_NAME = U.CONSTRAINT_NAME
                     where U.TABLE_NAME = p_parent
                       and u.constraint_Type = p_type
                       and nvl(R.TABLE_NAME,'-') = p_relative
                        and ( p_column is null or p_column = cc.column_name)
                    )
        loop
          dbms_output.put_line('alter table '||p_parent||' rename constraint '||rec.constraint_name||' to '||p_new_name);
          -- execute immediate 'alter table '||p_parent||' drop constraint '||rec.constraint_name;
        end loop;    
    end;
    
    procedure renamefk(p_parent varchar2, p_relative varchar2, p_column varchar2, p_new_name varchar2) is 
    begin
        rename_fk(p_parent, p_relative, p_column, p_new_name, 'R');
    end;
begin
    renamefk('comp_ent1','comp_ent2','ent2','comp_ent1_ent2'); -- SYS_id1
    --------------
    renamefk('comp_ent3','comp_ent4','ent4','comp_ent3_ent4'); -- SYS_id2
    --------------
    -- ~ 40 lines skipped
end