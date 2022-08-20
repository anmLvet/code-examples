         select DELIV_OrderSetID		as DeliveryID		
              , ProviderName
              , CourierName
              , COMP_OrderID	                as OrderID
              , OrderSetDate
              , OrderSetDescr
              , sum(pnTicket)		as pnTicket
              , sum(pNominal)		as pNominal
              , sum(pFee)		as pFee
              , case when (PaymentType = 'И' or PaymentType = 'Б') then 0
                        	else dbo.deliveryFee( COMP_OrderID )
                end 			as DeliveryFee
             , SaleDate
             , OrderSetName
             , Result
             , PaymentType as PaymentType
         from  ( SELECT 1 AS pnTicket
	              , case when (PaymentType = 'И' or PaymentType = 'Б') 
                                 then 0
		                 else COALESCE (T.Price, 0) - COALESCE (T.Fee, 0)
	                end  AS pNominal				
                  , case when (PaymentType = 'И' or PaymentType = 'Б') 
                             then 0					
                             else COALESCE (T.Fee,0) 
                    end AS pFee
                  , case when (PaymentType = 'И' or PaymentType = 'Б') 
                             then 0
                             else CDI.DeliveryFee
                    end as DeliveryFee
                  , CDI.COMP_DeliveryInfoID
	              , Adr.DELIV_OrderSetID
                  , T.COMP_OrderID
	              , P.OrderSetDate
	              , P.OrderSetDescr
                  , dbo.fdtDateOnly(T.SaleDate) AS SaleDate
	              , CONVERT(varchar(128), P.OrderSetDate, 104)+ ' '
                               + Pr.ProviderName + ' ' + COALESCE (C.CourierName, ' ') AS OrderSetName
	              , DR.DlvResult AS Result
	              , C.CourierName
                  , Pr.ProviderName
	              , PT.PaymentType
                   FROM dbo.COMP_DeliveryInfo CDI WITH (NOLOCK) 
             INNER JOIN dbo.COMP_DeliveryType DT WITH (NOLOCK)     ON CDI.COMP_DeliveryTypeID = DT.COMP_DeliveryTypeID 
             RIGHT JOIN dbo.COMP_Ticket T WITH (NOLOCK) 
              LEFT JOIN dbo.COMP_Order CO WITH (NOLOCK) 
             INNER JOIN dbo.COMP_SaleOutlet S         WITH(NOLOCK)          ON S.COMP_SaleOutletID = CO.SaleSaleOutletID 
             INNER JOIN dbo.C_VerticalRelation V WITH(NOLOCK)         
                              ON V.COMP_SaleTypeID = S.COMP_SaleTypeID and V.IfDeleted = 0 
                             AND V.C_VerticalRelationTypeID in (4,5,6,7) and V.C_CompanyID = @C_CompanyID
             INNER JOIN dbo.COMP_PaymentType PT WITH (NOLOCK)
                                ON CO.COMP_PaymentTypeID = PT.COMP_PaymentTypeID 
                                ON T.COMP_OrderID = CO.COMP_OrderID 
                                ON CDI.COMP_OrderID = T.COMP_OrderID 
             LEFT JOIN  dbo.DELIV_Address Adr WITH (NOLOCK) 
            RIGHT JOIN  dbo.DELIV_ExtDeliveryInfo ExtInfo WITH (NOLOCK) 
            INNER JOIN  dbo.DELIV_DeliveryResult DR WITH (NOLOCK) 
                                ON ExtInfo.DELIV_DeliveryResultID = DR.DELIV_DeliveryResultID 
                                ON Adr.DELIV_AddressID = ExtInfo.DELIV_AddressID 
            LEFT JOIN  dbo.DELIV_Courier C WITH (NOLOCK) 
           RIGHT JOIN  dbo.DELIV_OrderSet P WITH (NOLOCK) 
           INNER JOIN  dbo.DELIV_Provider PR WITH (NOLOCK)      ON P.DELIV_ProviderID           = Pr.DELIV_ProviderID
                                                              ON C.DELIV_CourierID            = P.DELIV_CourierID 
                                                              ON Adr.DELIV_OrderSetID          = P.DELIV_OrderSetID 
                                                              ON CDI.COMP_OrderID = ExtInfo.COMP_OrderID
                WHERE (PT.PaymentType<>'И') 
                     and  (PT.PaymentType<>'Б')  
                     and  (          (DT.DeliveryKind = 1) 
                                AND (CO.Accepted = 0) 
                                AND (CO.COMP_StatusID = 4) 
                                AND (T.COMP_StatusID = 4) 
                                AND (T.SaleDate < @dtTo)
                            )
                      OR (          (DT.DeliveryKind = 1) 
                               AND (CO.Accepted = 0) 
                               AND (CO.COMP_StatusID = 4) 
                               AND (T.COMP_StatusID = 4) 
                               AND (@dtTo IS NULL)
                            )
                 ) as dt
    group by DELIV_OrderSetID	

	, ProviderName	
	, CourierName	
	, COMP_OrderID
	, OrderSetDate		
	, OrderSetDescr	 	
	, SaleDate
	, OrderSetName
	, Result
	, PaymentType
   order by SaleDate
	, COMP_OrderID
