@description('Azure Front Door profile resource name')
param profileName string

@description('Azure region. Azure Front Door profiles are global resources; this is retained for module consistency.')
param location string = 'global'

@description('Azure Front Door endpoint name. This becomes <endpointName>.azurefd.net.')
param endpointName string

@description('Entra External ID custom URL domain host without scheme or path (e.g. customers.hartlabs.info)')
param customDomainHost string

@description('Entra External ID CIAM origin host without scheme or path (e.g. contoso.ciamlogin.com)')
param entraOriginHost string

@description('When true, associates the validated custom domain with the Front Door route. Deploy once with false, add DNS validation records, then redeploy with true.')
param enableCustomDomainAssociation bool = false

@description('Resource tags')
param tags object = {}

var originGroupName = 'og-entra-external-id'
var originName = 'origin-entra-external-id'
var routeName = 'route-entra-external-id'
var customDomainName = replace(customDomainHost, '.', '-')

resource profile 'Microsoft.Cdn/profiles@2024-09-01' = {
  name: profileName
  location: location
  tags: tags
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
}

resource endpoint 'Microsoft.Cdn/profiles/afdEndpoints@2024-09-01' = {
  parent: profile
  name: endpointName
  location: location
  tags: tags
  properties: {
    enabledState: 'Enabled'
  }
}

resource originGroup 'Microsoft.Cdn/profiles/originGroups@2024-09-01' = {
  parent: profile
  name: originGroupName
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    healthProbeSettings: {
      probePath: '/'
      probeRequestType: 'HEAD'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 100
    }
    sessionAffinityState: 'Disabled'
  }
}

resource origin 'Microsoft.Cdn/profiles/originGroups/origins@2024-09-01' = {
  parent: originGroup
  name: originName
  properties: {
    hostName: entraOriginHost
    originHostHeader: entraOriginHost
    httpPort: 80
    httpsPort: 443
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
  }
}

resource customDomain 'Microsoft.Cdn/profiles/customDomains@2024-09-01' = {
  parent: profile
  name: customDomainName
  properties: {
    hostName: customDomainHost
    tlsSettings: {
      certificateType: 'ManagedCertificate'
      minimumTlsVersion: 'TLS12'
    }
  }
}

resource route 'Microsoft.Cdn/profiles/afdEndpoints/routes@2024-09-01' = {
  parent: endpoint
  name: routeName
  properties: {
    originGroup: {
      id: originGroup.id
    }
    supportedProtocols: [
      'Http'
      'Https'
    ]
    patternsToMatch: [
      '/*'
    ]
    forwardingProtocol: 'HttpsOnly'
    httpsRedirect: 'Enabled'
    linkToDefaultDomain: enableCustomDomainAssociation ? 'Disabled' : 'Enabled'
    customDomains: enableCustomDomainAssociation ? [
      {
        id: customDomain.id
      }
    ] : []
    enabledState: 'Enabled'
  }
  dependsOn: [
    origin
  ]
}

output profileName string = profile.name
output endpointHostName string = endpoint.properties.hostName
output customDomainHostName string = customDomain.properties.hostName
output customDomainValidationToken string = customDomain.properties.validationProperties.validationToken
output customDomainAssociationEnabled bool = enableCustomDomainAssociation
