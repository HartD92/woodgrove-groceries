# Woodgrove groceries graph middleware

The purpose of the middleware is to facilitate the process of editing user’s profiles with multifactor authentication (MFA) protection. Here's a detailed explanation:

- The middleware acts as an intermediary layer between the [Woodgrove web app](https://woodgrovedemo.com) and the backend Microsoft Graph API services. 

- It ensures that only authenticated and authorized users can make changes to their profiles. It enforces the MFA requirement by integrating with Conditional Access policies. This adds an extra layer of security, ensuring that users must complete an MFA challenge before making any profile changes.

- The middleware is responsible for acquiring an access token on behalf of the user (User.ReadWrite delegated permission). This token is then used to call the Microsoft Graph API's /me endpoint to edit the user's profile.

  > [!IMPORTANT]
  > There is currently a permission issue with using delegated permissions. Therefore, the app now uses **application permissions** instead.
    
- The middleware handles communication with Microsoft Graph API. It sends requests to update user profiles and processes the responses.

- Finaly, it processes the Microsoft Graph API response and returns it to the Woodgrove web ap.

## Client application

This web API project has a single endpoint named [profile](./Controllers/ProfileController.cs). There two scenarios for calling this profile endpoint. 

- Woodgrove groceries web application [profile editing](https://woodgrovedemo.com/profile). In this flow, the profile editing page sends in the request payload all of the attributes that need to be updated. Attributes that are empty or null will remove the existing value in the user's profile.

- Another scenario is the Woodgrove groceries web application [chat page](https://woodgrovedemo.com/chat) (this is a private demonstration; access may be limited). In this flow, users update only one attribute at a time. The chat sets the DontSkipEmptyString attribute to false. As a result, the profile endpoint ignore empty attributes.

## Using the source code

The objective of the Woodgrove Groceries apps is to demonstrate various authentication user experiences. Although it is technically feasible to download and run the app, it's not recommended because the app is tailored to address specific scenarios related to Woodgrove demo requirements. 

Examples of Microsoft Entra External ID are available at <https://aka.ms/eeid/samples>. For these examples, we provide instructions on how to download and use the samples or create your own application based on common authentication and authorization scenarios, development languages, and platforms.

