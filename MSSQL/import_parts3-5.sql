CREATE PROCEDURE [dbo].[tmp_datetransfer_step3]
AS
BEGIN
declare @sqlquery nvarchar(max)

set @sqlquery = ''
PRINT ''Step 3 - Import from old DB. Start: ''+CONVERT(nvarchar(max), getdate(), 13)
PRINT ''''

PRINT ''3.1. Import account data.''
PRINT ''''

delete from lc_lp_account

    insert lc_lp_account 
         ( date
         , rectype
         , surname, name, middlename
         , email
         , comp_customerid
         , dateofassign2lp, dateofbecomespecial
         , originalFullName, originallogin
         )
    select getdate() as date
	    , case when (clientStatusID = 3)
			       then 2 -- Blocked
			       else 1 -- Active
	       end as rectype
	    , case when (len(FullName) = len(replace(FullName, '' '', '''')) + 2) 
				   then substring(FullName, 0, charindex('' '', FullName))
		           else null
		   end as surname
	    , case when (len(FullName) = len(replace(FullName, '' '', '''')) + 2)
			       then substring(FullName, charindex('' '', FullName) + 1, charindex('' '', FullName, charindex('' '', FullName) + 1) - charindex('' '', FullName) - 1)
		           else null
		   end as name
	    , case when (len(FullName) = len(replace(FullName, '' '', '''')) + 2)
			       then substring(FullName, charindex('' '', FullName, charindex('' '', FullName) + 1) + 1, len(FullName) - charindex('' '', FullName, charindex('' '', FullName) + 1))
		           else null
		   end as middlename
         , MailTo1        as email
         , loyal_clientID as comp_customerid -- temporarily save cliend id here
         , AddDate        as dateofassign2lp
	    , null           as dateofbecomespecial -- will be set at a later step
         , FullName       as originalFullName
         , Login          as originallogin
      from dbserver.oldDB.dbo.loyal_client PK 
inner join dbserver.oldDB.dbo.client K on K.clientID = PK.clientID
     where PK.loyal_clientID not in (19692, 19915, 18405, 29198)
  order by loyal_clientID

PRINT ''3.2. balance import''

delete from lc_balance

SET IDENTITY_INSERT lc_balance ON

insert into lc_balance (id, date, number, type, idstate, total, reserved, description, iscorp_client, oldidbalance)
     select cast(d.cardNumber as int)   as id
          , getdate()                   as date
          , d.cardNumber                as number
          , 1 as type
          , case when (clientStatusID = 3 or refuseTypeID = 5)
				  then 3
			       else case when (loyal_typeID = 0)
					             then case when (refuseTypeID = 4)
									         then 4
								              else 3
							         end
				                  else loyal_typeID
				       end
		  end                         as idstate
          , 0                           as total
          , 0                           as reserved
          , ''Imported from old DB''    as description
          , 0                           as iscorp_client
          , B.loyal_balanceID           as oldidbalance
       from dbserver.oldDB.dbo.loyal_balance B 
 inner join dbserver.oldDB.dbo.loyal_client Cl  on Cl.loyal_clientID = B.loyal_clientID 
                                                and ( Cl.clientStatusID <> 1 or Cl.loyal_typeID <> 0)
 inner join dbserver.oldDB.dbo.loyal_card d     on d.loyal_clientID = b.loyal_clientID
      where B.loyal_balanceID = ( select max(B1.loyal_balanceID) 
                                    from dbserver.oldDB.dbo.loyal_balance B1 
                                   where B1.loyal_clientID = B.loyal_clientID 
                                )
   order by d.cardNumber

SET IDENTITY_INSERT lc_balance OFF

declare @idbalancemax int
select @idbalancemax = max(id) + 1 from lc_balance
DBCC CHECKIDENT (''lc_balance'', RESEED, @idbalancemax)

PRINT ''	Reseeding ids in lc_balance to '' + cast(@idbalancemax as nvarchar(max))

PRINT ''	Import of cards and their issue dates''

delete from lc_balancecard

--SET IDENTITY_INSERT lc_balancecard ON

insert into lc_balancecard (idbalance, date, description)
select id as idbalance, getdate() as date, ''Imported from old DB'' as description
from lc_balance
where type = 1 and idstate != 3

--SET IDENTITY_INSERT lc_balancecard OFF

PRINT ''	Import of loyal accounts without cards''

insert into lc_balance (date, number, type, idstate, total, reserved, description, iscorp_client, oldidbalance)
     select getdate()                as date
		, ''''                     as number
		, 0                        as type -- no loyalty programs
		, case when (clientStatusID = 3 or refuseTypeID = 5)
				  then 3
			       else case when (loyal_typeID = 0)
						      then case when (refuseTypeID = 4)
									      then 4
								           else 3
							      end
				                else loyal_typeID
				       end
		  end                      as idstate
          , 0                        as total
		, 0                        as reserved
		, ''Imported from old DB'' as description
		, 0                        as iscorp_client
		, B.loyal_balanceID        as oldidbalance
       from dbserver.oldDB.dbo.loyal_balance B 
 inner join dbserver.oldDB.dbo.loyal_client Cl on Cl.loyal_clientID = B.loyal_clientID 
                                              and ( Cl.clientStatusID <> 1 or Cl.loyal_typeID <> 0)
  left join dbserver.oldDB.dbo.loyal_card d    on d.loyal_clientID = b.loyal_clientID
      where B.loyal_balanceID = ( select max(B1.loyal_balanceID) 
                                    from dbserver.oldDB.dbo.loyal_balance B1 
                                   where B1.loyal_clientID = B.loyal_clientID
                                )
        and d.cardNumber is null
   order by B.loyal_clientID

PRINT ''	Bonuses import''

delete from lc_balancehistory

insert into lc_balancehistory (idbalance, userlogin, date, amount, type,idreason, description, iscalc)
     select d.id as idbalance
          , ''oldcomp.admin'' as userlogin
          , getdate() as date
          , B.balance1 + B.balance2 as amount
          , 5 as type
          , 1 as idreason
          , ''Imported from old DB'' as description
          , 0 as iscalc
       from dbserver.oldDB.dbo.loyal_balance B 
 inner join lc_balance d on d.oldidbalance = B.loyal_balanceID
    --where B.loyal_balanceID = (select max(B1.loyal_balanceID) from dbserver.oldDB.dbo.loyal_balance B1 where B1.loyal_clientID = B.loyal_clientID)
   order by B.loyal_clientID

PRINT ''3.3. Linking accounts to balances''

delete from lc_balanceaccount

insert into lc_balanceaccount (idbalance, idaccount, isactive)
     select d.id as idbalance
          , e.lc_lp_accountid as idaccount
          , 1 as isactive
       from dbserver.oldDB.dbo.loyal_balance B
 inner join lc_balance d on d.oldidbalance = B.loyal_balanceID
 inner join lc_lp_account e on e.comp_customerid = B.loyal_clientID
      where B.loyal_clientID not in (19342, 19235, 18455, 29128)
   order by B.loyal_clientID

PRINT ''3.4. Accounts with more then one card.''
declare @currentaccountid int

PRINT ''	Client 1 card № 60001''

select @currentaccountid = lc_lp_accountid from lc_lp_account where comp_customerid = 16328

if (@currentaccountid is null)
	raiserror (''Client "Client 1" not found'', 16, 1)
else
	insert into lc_balanceaccount (idbalance, idaccount, isactive)
	values (60001, @currentaccountid, 1)

PRINT ''	Client 2 card № 61011''

select @currentaccountid = lc_lp_accountid from lc_lp_account where comp_customerid = 52364

if (@currentaccountid is null)
	raiserror (''Client "Client 2" not found'', 16, 1)
else
	insert into lc_balanceaccount (idbalance, idaccount, isactive)
	values (61011, @currentaccountid, 1)

PRINT ''	Client 3 card № 60922''

select @currentaccountid = lc_lp_accountid from lc_lp_account where comp_customerid = 59326

if (@currentaccountid is null)
	raiserror (''Client "Client 3" not found'', 16, 1)
else
	insert into lc_balanceaccount (idbalance, idaccount, isactive)
	values (60922, @currentaccountid, 1)

PRINT ''	Client 4 card № 60002''

select @currentaccountid = lc_lp_accountid from lc_lp_account where comp_customerid = 70237

if (@currentaccountid is null)
	raiserror (''Client "Client 4" not found'', 16, 1)
else
	insert into lc_balanceaccount (idbalance, idaccount, isactive)
	values (60002, @currentaccountid, 1)

-- Delete temporary data
update lc_lp_account set comp_customerid = null
--update lc_balance set description = ''Imported from old DB''

PRINT ''''
PRINT ''Step 3 - Import from old DB. End: ''+CONVERT(nvarchar(max), getdate(), 13)


exec sp_executesql @sqlquery
END
GO
CREATE PROCEDURE [dbo].[tmp_datetransfer_step4]
AS
BEGIN

PRINT 'Step 4 - Orders logging. Start: ' + CONVERT(nvarchar(max), getdate(), 13)
PRINT ''

declare @sqlquery1 nvarchar(max)

set @sqlquery1 = '
PRINT ''4.1. Adding bonuses for orders''
insert into dbo.lc_balancehistory(idbalance, idorder, userlogin, date, amount, type, idreason, description, iscalc)
     select b.id as idbalance
          , c.comp_orderid as idorder
          , ''oldcomp.admin'' as userlogin
          , isnull(c.saledate, getdate()) as date
          , case when (b.idstate = 1)	-- Regular
				  then case when (     dbo.fn_total_order_cost(c.comp_orderid)>=10000
                                       and dbo.deliveryFee(c.comp_orderid)>=200
                                     )
							  then sum(d.fee) / 5 + 200	--delivery fee
						       else sum(d.fee) / 5
					  end
			  when (b.idstate = 2)	-- Special
				  then case when (dbo.deliveryFee(c.comp_orderid)>=200)
							  then sum(d.fee) / 5 + 200	
						       else sum(d.fee) / 5
					  end 
		       else 0
		  end as amount
          , 5 as type
          , 2 as idreason
          , ''Imported from old DB'' as description
          , 0 as iscalc
       from lc_cardstoorders a 
 inner join lc_balance b         on b.id = a.cardnumber 
 inner join comp_order c         on c.comp_orderid = a.orderid 
 inner join comp_ticket d        on d.comp_orderid = a.orderid 
 inner join lc_balanceaccount e  on e.idbalance = b.id 
 inner join lc_lp_account f      on f.lc_lp_accountid = e.idaccount
 inner join comp_deliverytype dt on dt.comp_deliverytypeid = c.comp_deliverytypeid
      where b.type = 1		          -- loyal card type
	   and a.cardtype = 1			-- loyal card type
	   and a.iserased = 0			-- not deleted
	   and c.comp_salestatusid = 4	-- sold
        and (dt.deliverykind = 0 or c.accepted = 1) -- Either sold pickup orders, or accepted delivery orders
	   and d.comp_salestatusid = 4	-- sold
	   and e.isactive = 1			-- account link active
	   and f.rectype != 2		     -- account is not blocked
   group by c.comp_orderid, b.idstate, b.id, c.saledate

PRINT ''4.2. Linking client profiles to accounts''

create table #custaccount (idaccount int, idcustomer int)

insert into #custaccount (idaccount, idcustomer)
     select c.idaccount
          , b.comp_customerid
       from lc_balancehistory a 
 inner join comp_order b        on b.comp_orderid = a.idorder 
 inner join lc_balanceaccount c on c.idbalance = a.idbalance
      where a.idorder is not null 
        and c.isactive = 1
   group by c.idaccount, b.comp_customerid
   order by b.comp_customerid

--select a.*, (select count(1) from #custaccount b where b.idcustomer = a.idcustomer) as count 
--from #custaccount a
--where (select count(1) from #custaccount b where b.idcustomer = a.idcustomer) > 1
--order by a.idcustomer

if exists (select a.*
                , ( select count(1) 
                      from #custaccount b 
                     where b.idcustomer = a.idcustomer
                  ) as count 
		   from #custaccount a
		  where ( select count(1)
                      from #custaccount b 
                     where b.idcustomer = a.idcustomer 
                  ) > 1
	  --order by a.idcustomer
		) 
begin
	raiserror (''Found clients, who made orders via different loyal client accounts'', 16, 1)

	select a.*
          , ( select count(1) 
                from #custaccount b 
               where b.idcustomer = a.idcustomer
            ) as count 
	  from #custaccount a
	 where ( select count(1)
                from #custaccount b 
               where b.idcustomer = a.idcustomer
            ) > 1
end
else
begin
	PRINT ''	Clients, who made order via different loyal client accounts, were not found''
	
	insert into lc_account(idaccount, idcustomer, isactive, approved, approvedby)
	     select idaccount as idaccount
               , idcustomer as idcustomer
               , 1 as isactive
               , case when (lower(b.surname) = lower(c.customersurname) and lower(b.name) = lower(c.customername))
			            then 1
				       else 0
			  end as approved
               , case when (lower(b.surname) = lower(c.customersurname) and lower(b.name) = lower(c.customername))
				       then ''oldcomp.admin''
				       else null
			  end as approvedby
	       from #custaccount a 
      inner join lc_lp_account b on b.lc_lp_accountid = a.idaccount  
      inner join comp_customer c on c.comp_customerid = a.idcustomer
end

drop table #custaccount'

exec sp_executesql @sqlquery1

PRINT ''
PRINT 'Step 4 - Order logging. End: ' + CONVERT(nvarchar(max), getdate(), 13)

END
GO
CREATE PROCEDURE [dbo].[tmp_datetransfer_step5]
AS
BEGIN

declare @sqlquery2 nvarchar(max)

set @sqlquery2 = '
PRINT ''Step 5 - Creating accounts for companies. Start: '' + CONVERT(nvarchar(max), getdate(), 13)
PRINT ''''

PRINT ''5.1. Delete all blocked companies''

PRINT ''Companies to delete.''

select * from lc_corp_clients where isblocked = 1

delete from lc_corp_clients where isblocked = 1

PRINT ''5.2. Create accounts for companies''

-- rectype = 3 "no accrue", comp_customerid - temporary field for company id
insert into lc_lp_account (date, rectype, email, comp_customerid, originalFullName)
     select getdate()
          , 3
          , cast(email as varchar(50))
          , id
          , cast(name as varchar(128))
       from lc_corp_clients

-- Clear account-corpclient links
delete from lc_corp_clientaccount

-- Add account-corpclient links
insert into lc_corp_clientaccount (idaccount, idcorp_client, isactive)
     select lc_lp_accountid as idaccount
          , comp_customerid as idcorp_client
          , 1 as isactive
       from lc_lp_account
      where comp_customerid is not null

update lc_lp_account set comp_customerid = null

PRINT ''5.3. Create corporate loyal balances''

insert into lc_balance (date, type, idstate, total, reserved, description, iscorp_client)
     select getdate() as date
          , 3 as type
          , 1 as idstate
          , 0 as total
          , 0 as reserved
          , ''Corporate balance''
          , 1 as iscorp_client
       from lc_corp_clientaccount

insert into lc_balanceaccount (idaccount, idbalance, isactive)
     select a.idaccount
          , b.idbalance
          , 1 as isactive
       from ( select row_number() over (order by idaccount) as id
                   , idaccount as idaccount 
		      from lc_corp_clientaccount
            ) as a 
 inner join ( select row_number() over (order by id) as id
                   , id as idbalance
			 from lc_balance
		     where description = ''Corporate balance''
            ) b on b.id = a.id

/*	
PRINT ''5.4. Link employee accounts to companies''

create table #corpaccounts (idaccount int, idcorp_client int, isactive int)

insert into #corpaccounts(idaccount, idcorp_client, isactive)
     select a.id as idcorp_client
          , c.idaccount as idaccount
          , 1 as isactive
       from lc_orders a 
 inner join comp_order b on b.comp_orderid = a.coorderid
 inner join lc_account c on c.idcustomer = b.comp_customerid
      where c.isactive = 1 -- and a.coorderdate = (select max(coorderdate) from lc_orders where cocustomerid = a.cocustomerid)
   group by a.id, c.idaccount
   order by c.idaccount

select a.* from #corpaccounts a

drop table #corpaccounts
*/

PRINT ''''
PRINT ''Step 5 - Creating accounts for companies. End: '' + CONVERT(nvarchar(max), getdate(), 13)'

exec sp_executesql @sqlquery2

END