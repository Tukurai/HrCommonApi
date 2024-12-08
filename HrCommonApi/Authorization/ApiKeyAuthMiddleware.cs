﻿using HrCommonApi.Database;
using HrCommonApi.Database.Models;
using HrCommonApi.Services;
using HrCommonApi.Services.Base;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace HrCommonApi.Authorization;

/// <summary>
/// This middleware adds extra claims based on the API key header. It is only blocking is a claimed api key is not present or is invalid.
/// </summary>
public class ApiKeyAuthMiddleware<TApiKey>(RequestDelegate _next) where TApiKey : ApiKey
{
    public async Task InvokeAsync(HttpContext context, IApiKeyService<TApiKey> apiKeyService, IConfiguration configuration)
    {
        var apiKeyHeaderName = configuration["HrCommonApi:ApiKeyName"]!;
        var apiKeys = configuration.GetSection("HrCommonApi:AcceptedApiKeys").Get<List<string>>()!;

        if (context.Request.Headers.TryGetValue(apiKeyHeaderName, out var extractedApiKey))
        {
            if (!apiKeys.Contains(extractedApiKey!))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid API key");
                return;
            }

            var response = await apiKeyService.Authorize(extractedApiKey);
            if (response.Response != Enums.ServiceResponse.Success)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(response.Message);
                return;
            }

            if (response.Result!.Enabled)
            {
                var apiKeyIdentity = new ClaimsIdentity("ApiKey");
                apiKeyIdentity.AddClaim(new Claim(HrCommonApiKeyClaims.ApiKey, extractedApiKey!));

                foreach (var claim in response.Result!.Rights)
                    apiKeyIdentity.AddClaim(new Claim(claim.Name, claim.Value));

                context.User.AddIdentity(apiKeyIdentity);
            }
        }

        await _next(context);
    }
}
