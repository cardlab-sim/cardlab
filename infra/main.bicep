// CardLab demo environment (DEC-3, DEC-10, DEC-11). Skeleton from PR-00 so CI's `az bicep build` has a file;
// PR-03 replaces it with the App Service F1 plan, web app, Azure SQL free-offer database and firewall rule.
targetScope = 'resourceGroup'

@description('Azure region for every resource.')
param location string = resourceGroup().location

output location string = location
