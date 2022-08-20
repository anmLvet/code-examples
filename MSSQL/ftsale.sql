ALTER FUNCTION [dbo].[_ft_TicketSaleByPeriod_New]
(     
     @dtFrom datetime
,     @dtTo   datetime
,     @PromoterID int
)
RETURNS TABLE 
AS
     RETURN 
     (     

SELECT     
  dbo.COMP_Ticket.COMP_TicketID										AS TicketID
, CONVERT(varchar(32), dbo.COMP_Ticket.SaleDate, 102)			AS sSaleDate
, CONVERT(varchar(32), dbo.COMP_Ticket.CancelDate, 102)		AS sCancelDate
, dbo.COMP_Ticket.COMP_StatusID										AS TickeSateID
, dbo.COMP_Ticket.Price - dbo.COMP_Ticket.Fee	AS Price
, dbo.COMP_Ticket.Fee									AS Fee
, 1 AS nTicket
, dbo.COMP_Ticket.comp_showid
, comp_saleType.comp_saleTypeid as SaleTypeID
, case when COMP_Ticket.TicketSection is null or len(rtrim(COMP_Ticket.TicketSection)) = 0 then COMP_Ticket.TicketPriceCategoryName else COMP_Ticket.TicketSection end as TicketSection
FROM         dbo.COMP_Ticket WITH (NOLOCK) 
inner join comp_show aShow with(nolock) on aShow.comp_showid = comp_ticket.comp_showid
INNER JOIN dbo.COMP_Event with(nolock) ON aShow.COMP_EventID = dbo.COMP_Event.COMP_EventID
		inner join comp_saleOutlet  with(nolock) on comp_ticket.saleOutletid = comp_saleOutlet.comp_saleOutletid
		inner join comp_saleType  with(nolock) on comp_saleOutlet.comp_saleTypeid = comp_saleType.comp_saleTypeid
WHERE     (dbo.COMP_Ticket.SaleDate >= @dtFrom) 
AND (dbo.COMP_Ticket.SaleDate < @dtTo) 
AND (dbo.COMP_Ticket.COMP_StatusID IN (4, 7)) AND (dbo.COMP_Event.COMP_PromoterID = @PromoterID or @PromoterID is NULL)

     )
