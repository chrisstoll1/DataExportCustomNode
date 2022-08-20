# DataExportCustomNode

This GlobalAction custom node builds upon the existing data export node that comes pre-packaged with GlobalAction. It supports the same export types (CSV, SQL Server) and adds a few configuration options to better support the types of exports we do on a day to day basis. 

These new feautures include the following:
 - Change the delimiter - **CSV Only**
 - Specify which fields should be exported
 - Specify the order in which the exported columns should appear
 - Modify column names
 - System fields (DocID, ArchiveID, UniqueID) 
 - Apply .NET formatting to fields of a certain type (Date, Int, Float) 
 - Run regex replacements on exported field data
 - Support for GlobalSearch table fields

## Quickstart
Visit our [Wiki](https://github.com/chrisstoll1/DataExportCustomNode/wiki) to get started using this custom node
