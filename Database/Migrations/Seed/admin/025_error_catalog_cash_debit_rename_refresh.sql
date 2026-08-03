UPDATE admin.ErrorCatalog
SET Description = N'usp_Cash_DebitAndGrantItem: insufficient cash balance for this debit.'
WHERE ErrorNumber = 50240;

UPDATE admin.ErrorCatalog
SET Description = N'usp_Cash_DebitAndGrantItem/usp_Cash_Credit/usp_Cash_CreditAndConsumeItem: cash amount must be positive.'
WHERE ErrorNumber = 50241;
