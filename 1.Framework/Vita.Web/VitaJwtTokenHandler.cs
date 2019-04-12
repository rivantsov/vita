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
    SymmetricSecurityKey JwtKey; 

    public VitaJwtTokenHandler(EntityApp entityApp, IServiceCollection services, string jwtSecret) {
      _entityApp = entityApp;
      var secretBytes = Encoding.ASCII.GetBytes(jwtSecret);
      JwtKey = new SymmetricSecurityKey(secretBytes);
      SetupJwtAuthentication(services);
      _entityApp.RegisterService<IAuthenticationTokenHandler>(this);
    }

    private void SetupJwtAuthentication(IServiceCollection services) {
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
          IssuerSigningKey = JwtKey, 
          ValidateIssuer = false,
          ValidateAudience = false
        };
      });
    }

    public Task OnJwtTokenValidated(TokenValidatedContext context) {
      var webCtx = context.HttpContext.GetWebCallContext();
      webCtx.OperationContext.SetUserFromClaims(context.Principal.Claims);
      return Task.CompletedTask;
    }


  public string CreateToken(IList<Claim> claims, DateTime expires) {
      var tokenDescriptor = new SecurityTokenDescriptor {
        Subject = new ClaimsIdentity(claims),
        Expires = expires,
        SigningCredentials = new SigningCredentials(JwtKey, SecurityAlgorithms.HmacSha256Signature)
      };
      var tokenHandler = new JwtSecurityTokenHandler();
      var token = tokenHandler.CreateToken(tokenDescriptor);
      var tokenStr = tokenHandler.WriteToken(token);
      return tokenStr; 
    }

  }
}
