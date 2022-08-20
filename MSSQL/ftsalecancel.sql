ALTER FUNCTION [dbo].[_sales_and_cancels_by_period]
(     
     @dtFrom datetime
,     @dtTo   datetime
,     @PromoterID int
)
RETURNS @tt TABLE ( 
                           TicketID int
                         , Price          money default(0)
                         , Fee          money default(0)
                         , nTicket    int    default(0)
                         , pPrice          money default(0)
                         , pFee          money default(0)
                         , pnTicket    int    default(0)
                         , nPrice          money default(0)
                         , nFee          money default(0)
                         , nnTicket    int    default(0)
						 , comp_showid int default(0)
						 , SaleTypeID int default(0)
						 , TicketSection varchar(100)
                         , ActionDate varchar(32)
                         )
AS
begin

declare @tt1 TABLE (
                           TicketID int
                         , Price          money default(0)
                         , Fee          money default(0)
                         , nTicket    int    default(0)
                         , pPrice          money default(0)
                         , pFee          money default(0)
                         , pnTicket    int    default(0)
                         , nPrice          money default(0)
                         , nFee          money default(0)
                         , nnTicket    int    default(0)
						 , comp_showid int default(0)
						 , SaleTypeID int default(0)
						 , TicketSection varchar(100)
                         , ActionDate varchar(32)
                         )

     insert @tt1 (TicketSection, Price, Fee, nTicket, TicketID, pPrice, pFee, pnTicket, comp_showid, SaleTypeID, ActionDate) 
     select      TicketSection, Price, Fee, nTicket, TicketID, Price, Fee, nTicket, comp_showid, SaleTypeID, sSaleDate 
          from dbo._sales_by_period(@dtFrom, @dtTo, @PromoterID);

     insert @tt1 (TicketSection, Price, Fee, nTicket, TicketID, nPrice, nFee, nnTicket, comp_showid, SaleTypeID, ActionDate) 
     select      TicketSection, Price, Fee, nTicket, TicketID, Price, Fee, nTicket, comp_showid, SaleTypeID, sCancelDate 
          from dbo._cancels_by_period(@dtFrom, @dtTo, @PromoterID);

insert into @tt
select 
     TicketID 
     , sum(Price)
     , sum(Fee)
     , sum(nTicket)
     , sum(pPrice)
     , sum(pFee)
     , sum(pnTicket)
     , sum(nPrice)
     , sum(nFee)
     , sum(nnTicket)
	 , comp_showid
	 , SaleTypeID 
	 , TicketSection
     , ActionDate
from @tt1
     group by 
		TicketID
	 , comp_showid
	 , SaleTypeID 
	 , TicketSection
     , ActionDate
     return 

end  -- o-ee