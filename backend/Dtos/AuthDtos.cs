namespace MobileAlert.Api.Dtos;

// Los shapes de estos records tienen que calzar exacto con
// src/api/authApi.ts y src/types/branding.ts del lado de la app — hasta
// que ese contrato se saque a un paquete de tipos compartido (ver
// conversación sobre NestJS vs. esto), hay que tocar los dos lados a mano
// si uno cambia.

public record LoginRequestDto(string InstitutionCode, string Username, string Password);

public record LoginResponseDto(string Token, FirefighterDto Firefighter, BrandingDto Branding);

public record FirefighterDto(string Id, string Name, string Username);

public record BrandingDto(
    string InstitutionCode,
    string InstitutionName,
    string PrimaryColor,
    string? LogoUrl,
    string BackendUrl
);

public record RegisterDeviceRequestDto(string FcmToken);
