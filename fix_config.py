#!/usr/bin/env python3
"""
Quick script to disable problematic columns that cause data type conversion errors
"""
import json
import sys

# Columns that are causing type conversion errors
PROBLEMATIC_COLUMNS = {
    "Production.WorkOrderRouting": ["LocationID"],
    "Production.ProductModel": ["CatalogDescription"],
    "Purchasing.ProductVendor": ["LastReceiptCost", "LastReceiptDate"],
    "Person.StateProvince": ["StateProvinceID", "IsOnlyStateProvinceFlag"],
    "Sales.SalesOrderDetail": ["SalesOrderDetailID"],
    "Production.ProductInventory": ["LocationID"],
    "Production.ProductDescription": ["ProductDescriptionID"],
    "Production.ProductModelProductDescriptionCulture": ["ProductDescriptionID"],
    "Purchasing.PurchaseOrderDetail": ["PurchaseOrderDetailID"],
    "Sales.SalesPerson": ["SalesLastYear"],
    "Sales.SalesTerritory": ["SalesLastYear", "CostLastYear"],
    "Purchasing.ShipMethod": ["ShipMethodID", "ShipBase", "ShipRate"],
    "Purchasing.PurchaseOrderHeader": ["ShipMethodID", "ShipDate"],
    "Sales.SalesOrderHeader": ["ShipDate", "ShipMethodID"]
}

def fix_config(config_path):
    """Disable problematic columns in the configuration"""
    with open(config_path, 'r') as f:
        config = json.load(f)
    
    fixed_count = 0
    
    for table in config.get("Tables", []):
        table_name = table.get("TableName", "")
        if table_name in PROBLEMATIC_COLUMNS:
            problematic_cols = PROBLEMATIC_COLUMNS[table_name]
            
            for column in table.get("Columns", []):
                col_name = column.get("ColumnName", "")
                if col_name in problematic_cols:
                    column["Enabled"] = False
                    print(f"Disabled {table_name}.{col_name}")
                    fixed_count += 1
    
    # Write back the fixed configuration
    with open(config_path, 'w') as f:
        json.dump(config, f, indent=2)
    
    print(f"Fixed {fixed_count} problematic columns")
    return fixed_count

if __name__ == "__main__":
    config_file = "JSON/AdventureWorks2019.json"
    if len(sys.argv) > 1:
        config_file = sys.argv[1]
    
    fixed = fix_config(config_file)
    print(f"Configuration fixed: {config_file}")