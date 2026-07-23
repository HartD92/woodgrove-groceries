var disableAlert, signInAlert, mfaAlert, mfaFulfilled, verificationAlert;

$(document).ready(function () {

    disableAlert = new bootstrap.Modal(document.getElementById('disableAlert'), { keyboard: false });
    signInAlert = new bootstrap.Modal(document.getElementById('signInAlert'), { keyboard: false });
    mfaAlert = new bootstrap.Modal(document.getElementById('mfaAlert'), { keyboard: false });
    verificationAlert = new bootstrap.Modal(document.getElementById('verificationAlert'), { keyboard: false });

    // Check if MFA requirement has been fullfilled
    mfaFulfilled = ($("#MfaFulfilled").length == 1)

    // Get user profile
    getUserAttributes();
    getUserRoles();
    getUserMoreInfo();
    getPasskeys();

    // Enable or disable the profile attributes
    $("#inputCity").prop("disabled", !mfaFulfilled)
    $("#inputCountry").prop("disabled", !mfaFulfilled)
    $("#inputDisplayName").prop("disabled", !mfaFulfilled)
    $("#inputGivenName").prop("disabled", !mfaFulfilled)
    $("#inputSpecialDiet").prop("disabled", true)
    $("#inputSurname").prop("disabled", !mfaFulfilled)
    $("#inputEmailMfa").prop("disabled", !mfaFulfilled)
    $("#inputPhoneNumber").prop("disabled", !mfaFulfilled)
    $("#inputSignInEmail").prop("disabled", !mfaFulfilled)

    // Show or hide the edit profile buttonss
    if (mfaFulfilled) {
        $(".mfaFulfilled").show();
        $(".mfaRequiredButton").hide();
    }
    else {
        $(".mfaFulfilled").hide();
        $(".mfaRequiredButton").show();
    }
});

function disableAccount() {

    $("#disableAccountButtonSpinner").show();
    $.ajax({
        url: "/api/DisableAccount",
        success: function (result) {

            if (!result.errorMessage) {
                signInAlert.show();
            }
            else {
                $("#errorMessage").text(result.errorMessage);
                $("#errorMessageContainer").show();
            }

            $("#disableAccountButtonSpinner").hide();
        },
        // The function to execute when the request fails
        error: function (xhr, status, error) {
            console.log("/DisableAccount")
            console.log("Error: " + error);
            $("#disableAccountButtonSpinner").hide();
        }
    });

    // Hide the modal dialog
    disableAlert.hide();

}

function getUserAttributes() {
    $.ajax({
        url: "/api/userattributes",
        success: function (result) {

            if (!result.errorMessage) {
                $("#inputCity").val(result.city);
                $("#inputCountry").val(result.country);
                $("#inputDisplayName").val(result.displayName);
                $("#inputGivenName").val(result.givenName);
                $("#inputSpecialDiet").val(result.specialDiet);
                $("#inputSurname").val(result.surname);
                $('#inputAccountEnabled').prop('checked', result.accountEnabled);

                // Read only attributes
                $("#inputObjectID").text(result.objectId);
                $("#inputLastPasswordChangeDateTime").text(result.lastPasswordChangeDateTime);
                $("#inputCreatedDateTime").text(result.createdDateTime);

                // Show the editProfileSection
                $("#editProfileSection").show();
                $("#editProfileSpinner").hide();

            }
            else {

                $("#errorMessageContainer").show();

                if (result.errorMessage.includes("AcquireTokenSilent")) {
                    $("#errorMessage").text('Your access token is invalid. Please sign in!');
                    $("#signInButton").show();
                    $(".hideIfNoAuthenticated").hide();
                }
                else {
                    $("#errorMessage").text(result.errorMessage);
                }
            }

        },
        // The function to execute when the request fails
        error: function (xhr, status, error) {
            console.log("/UserAttributes")
            console.log("Error: " + error);
        }
    });
}

function updateUserAttributes() {

    $("#editProfileButtonSpinner").show();
    $("#errorMessageContainer").hide();
    $("#editProfileButton").prop("disabled", true);

    var payload = {
        city: $("#inputCity").val(),
        country: $("#inputCountry").val(),
        displayName: $("#inputDisplayName").val(),
        givenName: $("#inputGivenName").val(),
        specialDiet: $("#inputSpecialDiet").val(),
        surname: $("#inputSurname").val(),
        accountEnabled: $('#inputAccountEnabled').prop('checked')
    }

    $.post("/api/userattributes", payload, function (result) {

        $("#editProfileButtonSpinner").hide();
        $("#editProfileButton").prop("disabled", false);

        // Convert the result to a JSON object
        if (typeof result === "string") {
            result = JSON.parse(result);
        }

        // Check if the result contains an error message
        if (result.errorMessage) {
            $("#errorMessage").text(result.errorMessage);
            $("#errorMessageContainer").show();
        }
        else {
            // If no error, show the request to sign to updat the access token
            signInAlert.show();
        }

    });
}

function getUserRoles() {
    $.ajax({
        url: "/api/userroles",
        success: function (result) {

            if (!result.errorMessage) {
                $('#inputMemberOfCommercialAccounts').prop('checked', result.memberOfCommercialAccounts);
                $('#inputHasProductsContributorRole').prop('checked', result.hasProductsContributorRole);
                $('#inputHasOrdersManagerRole').prop('checked', result.hasOrdersManagerRole);

                // Show the editProfileSection
                $("#rolesSection").show();
                $("#rolesSpinner").hide();
            }
            else {
                $("#errorMessage").text(result.errorMessage);
                $("#errorMessageContainer").show();
            }

        },
        // The function to execute when the request fails
        error: function (xhr, status, error) {
            console.log("/UserRoles")
            console.log("Error: " + error);
        }
    });
}

function updateUserRoles() {

    $("#rolesButtonSpinner").show();
    $("#rolesButton").prop("disabled", true);

    var payload = {
        memberOfCommercialAccounts: $('#inputMemberOfCommercialAccounts').prop('checked'),
        hasProductsContributorRole: $('#inputHasProductsContributorRole').prop('checked'),
        hasOrdersManagerRole: $('#inputHasOrdersManagerRole').prop('checked')
    }

    $.post("/api/userroles", payload, function (result) {

        $("#rolesButtonSpinner").hide();
        $("#rolesButton").prop("disabled", false);

        signInAlert.show()
    });
}

function getUserMoreInfo() {
    $.ajax({
        url: "/api/usermoreinfo",
        success: function (result) {

            if (!result.errorMessage) {
                $("#inputIdentities").html(result.identities);

                // Sign-in name
                $("#inputSignInEmail").val(result.singInEmail);

                // MFA authentication methods
                $("#inputEmailMfa").val(result.emailMfa);
                $("#inputPhoneNumber").val(result.phoneNumber);

                // Activity
                $("#inputLastSignInDateTime").text(result.lastSignInDateTime);
                $("#inputLastSignInRequestId").text(result.lastSignInRequestId);

                // Show the sign-in name section
                if (result.singInEmail && result.singInEmail != '') {
                    $("#singInSection").show();
                    $("#singInSpinner").hide();
                }
                else {
                    // Hide for social accounts
                    $("#singInContainer").hide();
                }

                // Show the MFA section
                $("#mfaSection").show();
                $("#mfaSpinner").hide();
            }
            else {
                $("#errorMessage").text(result.errorMessage);
                $("#errorMessageContainer").show();
            }

        },
        // The function to execute when the request fails
        error: function (xhr, status, error) {
            console.log("/UserMoreInfo")
            console.log("Error: " + error);
        }
    });
}

function sendCodeForSignInEmail() {

    // Set up UI elements
    $('#inputVerificationCode').val("");
    $('#verificationError').text("")
    $('#verificationSpinner').show();
    $('#verificationContainer').hide();
    $('#verificationButtonSpinner').hide();

    // Show the alert
    verificationAlert.show();

    var payload = {
        AuthValue: $('#inputSignInEmail').val(),
        AuthType: 0
    }

    $.ajax({
        url: "/api/SendCode",
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
        success: function (result) {

            // Show the UI elements
            $('#verificationSpinner').hide();
            $('#verificationContainer').show();

            if (result.error) {
                // Show the error message
                $("#verificationError").text(result.error);
            }

        },
        // The function to execute when the request fails
        error: function (xhr, status, error) {
            console.log("/SendCode")
            console.log("Error: " + error);
            $("#verificationError").text(error);
        }
    });
}


function sendCodeForEmailMfa() {

    // Clear authentication method
    if ($('#inputEmailMfa').val().trim() == '') {
        return;
    }

    // Set up UI elements
    $('#inputVerificationCode').val("");
    $('#verificationError').text("")
    $('#verificationSpinner').show();
    $('#verificationContainer').hide();
    $('#verificationButtonSpinner').hide();

    // Show the alert
    verificationAlert.show();

    var payload = {
        AuthValue: $('#inputEmailMfa').val(),
        AuthType: 1
    }

    $.ajax({
        url: "/api/SendCode",
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
        success: function (result) {

            // Show the UI elements
            $('#verificationSpinner').hide();
            $('#verificationContainer').show();

            if (result.error) {
                console.log("/SendCode")
                console.log("Error: " + error);
                $("#verificationError").text(error);
            }
        }
    });
}

function verifyCode() {

    $('#verificationButtonSpinner').show();

    var payload = {
        VerificationCode: $('#inputVerificationCode').val()
    }

    $.ajax({
        url: "/api/VerifyCode",
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
        success: function (result) {

            $('#verificationButtonSpinner').hide();

            if (!result.error) {

                if (result.validationPassed) {
                    verificationAlert.hide();
                }
                else {
                    $("#verificationError").text("Invalid code");
                }

            }
            else {
                $("#verificationError").text(result.error);
            }

        },
        // The function to execute when the request fails
        error: function (xhr, status, error) {
            console.log("/VerifyCode")
            console.log("Error: " + error);
            $("#verificationError").text(error);
        }
    });
}

function showProfileError(message) {
    $("#errorMessage").text(message);
    $("#errorMessageContainer").show();
}

function clearProfileError() {
    $("#errorMessageContainer").hide();
    $("#errorMessage").text("");
}

function getPasskeys() {
    $.ajax({
        url: "/api/passkeys",
        success: function (result) {
            $("#passkeysSpinner").hide();
            $("#passkeysSection").show();

            if (result.errorMessage) {
                showProfileError(result.errorMessage);
                $("#passkeyEmptyState").show();
                $("#passkeysTableContainer").hide();
                return;
            }

            renderPasskeys(result.passkeys || []);
        },
        error: function (xhr, status, error) {
            $("#passkeysSpinner").hide();
            $("#passkeysSection").show();
            showProfileError(error);
        }
    });
}

function renderPasskeys(passkeys) {
    const body = $("#passkeysTableBody");
    body.empty();

    if (!passkeys || passkeys.length === 0) {
        $("#passkeyEmptyState").show();
        $("#passkeysTableContainer").hide();
        return;
    }

    $("#passkeyEmptyState").hide();
    $("#passkeysTableContainer").show();

    for (const passkey of passkeys) {
        const row = $("<tr></tr>");
        row.append(`<td>${escapeHtml(passkey.displayName || "")}</td>`);
        row.append(`<td>${escapeHtml(passkey.passkeyType || "")}</td>`);
        row.append(`<td>${escapeHtml(passkey.model || "")}</td>`);
        row.append(`<td>${escapeHtml(formatDateTime(passkey.createdDateTime))}</td>`);
        row.append(`<td>${escapeHtml(formatDateTime(passkey.lastUsedDateTime))}</td>`);

        if (mfaFulfilled) {
            row.append(`<td><button type="button" class="btn btn-sm btn-outline-danger" onclick="deletePasskey('${escapeAttribute(passkey.id || "")}')">Delete</button></td>`);
        } else {
            row.append(`<td><button type="button" class="btn btn-sm btn-outline-secondary" data-bs-toggle="modal" data-bs-target="#mfaAlert">Delete</button></td>`);
        }

        body.append(row);
    }
}

async function registerPasskey() {
    clearProfileError();
    $("#registerPasskeyButton").prop("disabled", true);
    $("#registerPasskeySpinner").show();

    try {
        const creationOptionsResponse = await fetch("/api/passkeys/creation-options");
        const creationOptions = await creationOptionsResponse.json();

        if (creationOptions.errorMessage) {
            showProfileError(creationOptions.errorMessage);
            return;
        }

        if (!window.PublicKeyCredential || !navigator.credentials) {
            showProfileError("This browser doesn't support passkeys.");
            return;
        }

        const publicKey = buildPublicKeyOptions(creationOptions);
        const credential = await navigator.credentials.create({ publicKey: publicKey });

        const payload = {
            publicKeyCredential: {
                id: credential.id,
                response: {
                    attestationObject: bufferToBase64url(credential.response.attestationObject),
                    clientDataJSON: bufferToBase64url(credential.response.clientDataJSON)
                }
            }
        };

        const registerResponse = await fetch("/api/passkeys/register", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const registerResult = await registerResponse.json();
        if (registerResult.errorMessage) {
            showProfileError(registerResult.errorMessage);
            return;
        }

        getPasskeys();
    } catch (error) {
        showProfileError(error.message || "Passkey registration failed.");
    } finally {
        $("#registerPasskeyButton").prop("disabled", false);
        $("#registerPasskeySpinner").hide();
    }
}

async function deletePasskey(passkeyId) {
    clearProfileError();
    if (!passkeyId) {
        return;
    }

    if (!confirm("Delete this passkey?")) {
        return;
    }

    try {
        const response = await fetch(`/api/passkeys/${encodeURIComponent(passkeyId)}`, {
            method: "DELETE"
        });
        const result = await response.json();
        if (result.errorMessage) {
            showProfileError(result.errorMessage);
            return;
        }

        getPasskeys();
    } catch (error) {
        showProfileError(error.message || "Passkey deletion failed.");
    }
}

function buildPublicKeyOptions(creationOptions) {
    const excludeCredentials = (creationOptions.excludeCredentials || []).map(c => ({
        ...c,
        id: decodeGraphCredentialId(c.id)
    }));

    return {
        challenge: base64urlToBuffer(creationOptions.challenge),
        rp: creationOptions.rp,
        user: {
            ...creationOptions.user,
            id: base64urlToBuffer(creationOptions.user.id)
        },
        pubKeyCredParams: creationOptions.pubKeyCredParams,
        timeout: creationOptions.timeout,
        excludeCredentials: excludeCredentials,
        authenticatorSelection: creationOptions.authenticatorSelection,
        attestation: creationOptions.attestation
    };
}

function base64urlToBuffer(base64url) {
    const padding = "=".repeat((4 - (base64url.length % 4)) % 4);
    const base64 = (base64url + padding).replace(/-/g, "+").replace(/_/g, "/");
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes.buffer;
}

function bufferToBase64url(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = "";
    for (let i = 0; i < bytes.length; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function decodeGraphCredentialId(id) {
    // Microsoft Graph returns excludeCredentials[].id as a base64url string,
    // the same encoding used for challenge and user.id. Decode it consistently
    // so credential IDs ending in a digit aren't corrupted.
    return base64urlToBuffer(id);
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

function escapeAttribute(value) {
    return String(value)
        .replaceAll("\\", "\\\\")
        .replaceAll("'", "\\'");
}

function formatDateTime(value) {
    if (!value) {
        return "";
    }

    const date = new Date(value);
    if (isNaN(date.getTime())) {
        return value;
    }

    return date.toLocaleString();
}
