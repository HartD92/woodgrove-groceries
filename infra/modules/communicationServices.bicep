// Azure Communication Services (ACS) + Email Service with an Azure-managed domain.
//
// ACS resources must be deployed to location='global'; physical data residency is
// controlled by the dataLocation property.
//
// The Azure-managed domain provides a free @azurecomm.net sender address suitable
// for dev/test.  For production, replace the AzureManagedDomain resource with a
// CustomerManaged domain pointing to your verified DNS zone and remove the
// linkedDomains reference below, then update it after domain verification.
//
// TODO (post-deploy):  For a custom domain, provision
//   Microsoft.Communication/emailServices/domains@2023-04-01 with
//   domainManagement: 'CustomerManaged', verify DNS TXT ownership, then link it to
//   the ACS resource by re-running this deployment with the verified domain resource ID.

@description('ACS resource name — must be globally unique')
param name string

@description('Email Communication Services resource name')
param emailServiceName string

@description('Data residency location (e.g. "United States", "Europe")')
param dataLocation string = 'United States'

@description('Resource tags')
param tags object = {}

// Email Service + Azure-managed domain must be created before ACS so we can link them.
resource emailService 'Microsoft.Communication/emailServices@2023-04-01' = {
  name: emailServiceName
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
  }
}

// Azure-managed domain — name MUST be 'AzureManagedDomain' for AzureManaged type.
resource azureManagedDomain 'Microsoft.Communication/emailServices/domains@2023-04-01' = {
  parent: emailService
  name: 'AzureManagedDomain'
  location: 'global'
  properties: {
    domainManagement: 'AzureManaged'
  }
}

// ACS resource linked to the email domain so it can send email OTPs.
resource acs 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: name
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
    linkedDomains: [
      azureManagedDomain.id
    ]
  }
}

output id string = acs.id
output name string = acs.name
output emailServiceId string = emailService.id
output emailServiceName string = emailService.name
output managedDomainId string = azureManagedDomain.id
