using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vita.Entities;
using Vita.Entities.Api;

namespace Vita.Web {
  public interface IAuthenticationTokenCreator {
    string CreateToken(IList<Claim> claims, DateTime expires);
  }

  public class VitaJwtTokenHandler : IAuthenticationTokenCreator {
    byte[] JwtSecretBytes;
    SymmetricSecurityKey JwtKey; 

    public VitaJwtTokenHandler(string jwtSecret) {
      JwtSecretBytes = Encoding.ASCII.GetBytes(jwtSecret);
      JwtKey = new SymmetricSecurityKey(JwtSecretBytes);
    }

    public void SetupJwtAuthentication(IServiceCollection services, EntityApp entityApp) {
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
      entityApp.RegisterService<IAuthenticationTokenCreator>(this); 
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
