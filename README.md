# AzureFileShare

A simple web app for uploading files to an Azure blob storage account and getting a read-only download link that can be shared for easy download by others.

## Overview
* Server-side logic implemented as an Azure Function that can run in consumption mode for minimal charges
  * Authentication is via Azure AD authentication using [App Service EasyAuth](https://docs.microsoft.com/en-us/azure/app-service/overview-authentication-authorization)
  * All users of the configured AAD tenant are granted access to upload files
* Files are stored to an Azure blob storage account
  * The blob container must be configured for public read access to allow download without authentication
  * A random "upload ID" is added to the file path to prevent download by persons without the full download link
  * Files are uploaded directly to the blob container using SAS tokens that are issued by the Azure Function
  * Blob [lifecycle management policies](https://docs.microsoft.com/en-us/azure/storage/blobs/storage-lifecycle-management-concepts) can be configured to automatically delete or archive files when they reach a certain age
* Web UI:
  * Single page web app with client-side JS that calls the Azure Function and uses the Azure Storage JavaScript Client Library for Browsers to upload the file
  * Files are uploaded via browser or azcopy
  * The HTML and JS files are stored in a blob container and served via a functions proxy so that static content as well as API are hosted under the same domain
  * The client-side JS does not use any frameworks and makes use of Fetch API and Promises 
