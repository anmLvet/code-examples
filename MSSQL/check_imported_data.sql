declare @amountDB decimal (18, 2)
declare @amountShDB decimal (18, 2)

    select @amountDB = sum(b.amount)
      from lc_balance a 
inner join lc_balancechange b on b.idbalance = a.id
     where b.idorder is null

    select @amountShDB = sum(b.new_balance + b.new_balance2)
      from lc_balance a 
inner join EXTSERVER.EXTdb.dbo.loyal_client b on b.loyal_client_id = a.oldidbalance

create table #balancechange (idorder int, amount decimal (18, 2))

insert into #balancechange(idorder, amount)
     select	b.comp_orderid as idorder
          , case when (e.idstate = 1) -- General
			         then case when (dbo.FN_TOTAL_ORDER_COST(b.comp_orderid) >= 10000)
							       then ( select sum(Fee) 
                                            from dbo.COMP_Ticket 
                                           where COMP_OrderID = b.comp_orderid 
                                             and COMP_StatusID = 4 ) / 5 + 200
						           else ( select sum(Fee) 
                                            from dbo.COMP_Ticket 
                                           where COMP_OrderID = b.comp_orderid 
                                             and COMP_StatusID = 4) / 5
					      end
        		when (e.idstate = 2) -- Special
				    then ( select sum(Fee) 
                             from dbo.COMP_Ticket 
                            where COMP_OrderID = b.comp_orderid 
                              and COMP_StatusID = 4) / 5
		    end as amount
       from	lc_cardstoorders a 
 inner join comp_order b on b.comp_orderid = a.extorderid 
 inner join lc_balance e on e.id = a.CardNumber 
 inner join lc_customeraccount f on f.idcustomer = b.comp_customerid 
 inner join lc_balanceaccount j on j.idaccount = f.idaccount 
 inner join lc_lp_account h on h.lc_lp_accountid = j.idaccount
      where	a.iserased = 0 
        and a.cardtype = 1 
        and b.comp_salestatusid = 4 
        and (e.idstate = 1 or e.idstate = 2)
		and j.idbalance = a.cardnumber 
        and h.accounttype != 2

declare @cardbalancetotal decimal (18, 2)
declare @cardtotal decimal (18, 2)
declare @cbequal bit

select @cardtotal = sum(amount) from #balancechange
select @cardbalancetotal = sum (amount) from lc_balancechange where idorder is not null

if (exists(	 select	a.idorder
                  , a.amount as lastamount
                  , b.amount as amount
                  , d.comp_customerid
                  , d.customername
                  , d.customersurname
                  , d.customeremail
			   from	#balancechange a 
          left join lc_balancechange b on b.idorder = a.idorder 
         inner join comp_order c on c.comp_orderid = a.idorder 
         inner join comp_customer d on d.comp_customerid = c.comp_customerid
			  where b.id is null 
                 or a.amount != b.amount
			)
	) set @cbequal = 0
else
	set @cbequal = 1

PRINT '8. Check imported data'

select 'Check data imported from old DB' as description
     , @amountShDB as lastamount
     , @amountDB as amount
     , case when (@amountDB = @amountShDB) 
                then 1 
                else 0 
       end as isequal
union 
select 'Loyalty bonuses amount check' as description
     , @cardtotal as lastamount
     , @cardbalancetotal as amount
     , @cbequal as equal 

drop table #balancechange