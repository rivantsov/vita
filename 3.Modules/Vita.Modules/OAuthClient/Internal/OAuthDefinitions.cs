using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Entities.Web;

namespace Vita.Modules.OAuthClient.Internal {

  public class AccessTokenResponse {
    [Node("access_token")]
    public string AccessToken;
    [Node("expires_in")]
    public int ExpiresIn;
    [Node("token_type")]
    public string TokenType;
    [Node("refresh_token")]
    public string RefreshToken;
    [Node("id_token")]
    public string IdToken; //Open ID connect only
  }

  public class OpenIdToken {
    [Node("sub")]
    public string Subject;
    [Node("iss")]
    public string Issuer;
    [Node("aud")]
    public string Audience;
    [Node("nonce")]
    public string Nonce;
    [Node("auth_time")]
    public string AuthTime;
    //  authentication context reference
    [Node("acr")]
    public string ContextRef;
    [Node("iat")]
    public string IssuedAt;
    [Node("exp")]
    public string ExpiresAt;
  }


  // Params passed with Redirect in URL; should be properties, not fields!  
  public class OAuthRedirectParams {
    public string Error { get; set; }  // not empty if error
    public string Code { get; set; }  //access code
    public string State { get; set; } //passed from AuthURL, flowId
  }

  public enum OpenIdScopes {
    Email,
    Profile,
    Address,
    Phone
  }

  public static class OpenIdClaims {
    public static class Profile {
      public const string Scope = "profile";
      // name, family_name, given_name, middle_name, nickname, preferred_username, profile, 
      //  picture, website, gender, birthdate, zoneinfo, locale, updated_at
      public const string Name = "name";
      public const string FamilyName = "family_name";
      public const string GivenName = "given_name";
      //etc.
    }// class
    public static class Email {
      public const string Scope = "email";
      public const string EmailClaim = "email";
      public const string EmailVerified = "email_verified";
    }
    public static class Address {
      public const string Scope = "address";
      public const string AddressClaim = "address";
    }
    public static class Phone {
      public const string Scope = "phone";
      public const string PhoneNumber = "phone_number";
      public const string PhoneNumberVerified = "phone_number_verified";
    }
  }

  public static class OAuthTemplates {

    /// <summary>The query (parameters) portion of authorization URL - a page on OAuth server 
    /// that user is shown to approve the access by the client app. </summary>
    public const string AuthorizationUrlQuery = "?response_type=code&client_id={0}&redirect_uri={1}&scope={2}&state={3}";

    public const string OpenIdClaimsParameter = "claims={0}";

    /// <summary>The query (parameters) portion of get-access-token URL.</summary>
    public const string GetAccessTokenUrlQuery = 
      "?code={0}&client_id={1}&client_secret={2}&redirect_uri={3}&grant_type=authorization_code";

  }

}
