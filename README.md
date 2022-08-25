# code-examples
This repository contains code samples from previous projects and jobs. Code was obfuscated due to confidentiality agreements. This repository doesn't contain complete projects.

**MSSQL** - MSSQL/T-SQL code examples. Scripts for reports, and importing data from previous database<br>
**.Net/WinForms** - A selection of code from the module, providing return invoices functionality. Code here contains partly abridged WinForms+DevExpress main form of the module and supporting it class, providing business logic.<br>

Return invoices is a document that informs event's promoter, which tickets weren't sold by agency in time, so that promoter could try selling them themselves. Generation and sending this document is a time-critical process, which occurs sometimes in a span of couple of hours between agency's sales end and actual event start - and as earlier the return invoice is sent, the better.<br>

During this time, first, all database synchronizations should have taken place (part of them were outside of our team's control), and then manual check was required, because due to inconsistencies in data formats, there was no guarantee, that return invoice content would be automatically generated correctly in 100% cases. However due to obvious financial component, 100% exactness of return invoice in the end was required.<br>

While most parts of business process were provided to me as technical requirements, it was my responsibility to fill in all the gaps, including interaction with main HQ, as well as provide a complete product that allowed to streamline this process as much as possible given specific conditions.<br>
**.Net/WebServices** - Collection of methods, performing various tasks in Oracle database, using ODP.NET, EF 5 and partly legacy ADO.NET code. Includes a helper class, simplyfying several operations.<br>
**.NET/WPF/PatchWebService** - WPF interface for a simple app for comparing versions of legacy webservices and patching them<br>
**.NET/WPF/SignApp** - simple WPF interface for an app for automatically adding signatures to documents<br>
**Oracle** - Oracle code examples: batch renaming foreign keys, integrity checks, xml parsing, metadata update example
