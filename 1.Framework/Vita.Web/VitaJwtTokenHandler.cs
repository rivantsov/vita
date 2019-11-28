using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

using Vita.Entities;

namespace Vita.Web {
  public interface IAuthenticationTokenHandler {
    string CreateToken(IList<Claim> claims, DateTime expires);
  }

  public class VitaJwtTokenHandler : IAuthenticationTokenHandler {
    EntityApp _entityApp;
    SymmetricSecurityKey _jwtKey; 

    public VitaJwtTokenHandler(EntityApp entityApp, IServiceCollection services, string jwtSecret) {
      _entityApp = entityApp;
      _entityApp.RegisterService<IAuthenticationTokenHandler>(this);
      var secretBytes = Encoding.ASCII.GetBytes(jwtSecret);
      _jwtKey = new SymmetricSecurityKey(secretBytes);
      // some cryptic code copied from samples somewhere
      services.AddAuthentication(x => {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
      })
      .AddJwtBearer(x => {
        x.Events = new JwtBearerEvents {
          OnTokenValidated = OnJwtTokenValidated
        };
        
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters {
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = _jwtKey, 
          ValidateIssuer = false,
          ValidateAudience = false
        };
      });
    }

    public Task OnJwtTokenValidated(TokenValidatedContext context) {
      var webCtx = context.HttpContext.GetWebCallContext();
      SetUserFromClaims(webCtx.OperationContext, context.Principal.Claims);
      return Task.CompletedTask;
    }


  public string CreateToken(IList<Claim> claims, DateTime expires) {
      var tokenDescriptor = new SecurityTokenDescriptor {
        Subject = new ClaimsIdentity(claims),
        Expires = expires,
        SigningCredentials = new SigningCredentials(_jwtKey, SecurityAlgorithms.HmacSha256Signature)
      };
      var tokenHandler = new JwtSecurityTokenHandler();
      var token = tokenHandler.CreateToken(tokenDescriptor);
      var tokenStr = tokenHandler.WriteToken(token);
      return tokenStr; 
    }

    private void SetUserFromClaims(OperationContext context, IEnumerable<Claim> claims) {
      Guid userId = Guid.Empty;
      string userName = string.Empty;
      long altUserId = 0;
      foreach (var claim in claims) {
        var v = claim.Value;
        switch (claim.Type) {
          case nameof(UserInfo.UserId):
            Guid.TryParse(claim.Value, out userId);
            break;
          case nameof(UserInfo.UserName):
            userName = claim.Value;
            break;
          case nameof(UserInfo.AltUserId):
            long.TryParse(claim.Value, out altUserId);
            break;
        } //switch
      } //foreach
      // Set UserInfo on current operation context
      context.User = new UserInfo(userId, userName, UserKind.AuthenticatedUser, altUserId);
    }


  }
}
