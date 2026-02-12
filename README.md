# Digital Medical Records System (DMRS) نظام السجلات الطبية الرقمية

A comprehensive digital medical records management system built with ASP.NET Core Web API, implementing FHIR (Fast Healthcare Interoperability Resources) standards for healthcare data interoperability.
نظام شامل لإدارة السجلات الطبية الرقمية تم بناؤه باستخدام ASP.NET Core Web API، ويطبق معايير FHIR (موارد الرعاية الصحية السريعة للتشغيل البيني) لضمان تبادل بيانات الرعاية الصحية وقابلية التشغيل البيني.

## Overview

DMRS is a modern, standards-based healthcare information system designed to manage patient medical records securely and efficiently. The system leverages the HL7 FHIR specification to ensure interoperability with other healthcare systems while maintaining robust security through OAuth2 authentication.

يُعد DMRS نظاماً حديثاً لمعلومات الرعاية الصحية يعتمد على المعايير القياسية، وقد صُمم لإدارة السجلات الطبية للمرضى بشكل آمن وفعال. يستفيد النظام من مواصفات HL7 FHIR لضمان قابلية التشغيل البيني (Interoperability) مع أنظمة الرعاية الصحية الأخرى، مع الحفاظ على مستوى عالٍ من الأمان من خلال استخدام بروتوكول المصادقة OAuth2.

## Features

- **FHIR-Compliant**: Full implementation of HL7 FHIR specification using Firely SDK
- **Secure Authentication**: OAuth2/OpenID Connect via Keycloak integration
- **RESTful API**: Clean, well-documented API endpoints following REST principles
- **Data Validation**: Comprehensive validation using FluentValidation and FHIR specifications
- **Modern UI**: Interactive Blazor-based front-end application
- **Database**: PostgreSQL for reliable data persistence
- **Entity Framework Core**: Code-first database approach with migrations

## Tech Stack

### Backend

- **Framework**: ASP.NET Core Web API
- **FHIR Library**: HL7.Fhir.NET (Firely SDK)
- **Database**: PostgreSQL
- **ORM**: Entity Framework Core
- **Validation**: FluentValidation
- **Authentication**: Keycloak (OAuth2/OpenID Connect)

### Frontend

- **Framework**: Blazor

## Prerequisites

Before you begin, ensure you have the following installed:

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- [PostgreSQL](https://www.postgresql.org/download/) (version 12 or higher)
- [Keycloak](https://www.keycloak.org/downloads) (version 20 or higher)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/Khair69/DMRS.git
cd DMRS
```

### 2. Database Setup

#### Create PostgreSQL Database

```bash
createdb dmrs_db
```

#### Update Connection String

Update the connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=dmrs_db;Username=your_username;Password=your_password"
  }
}
```

#### Run Migrations

```bash
dotnet ef database update
```

### 3. Keycloak Configuration

#### Start Keycloak

```bash
# Using Docker
docker run -p 8080:8080 -e KEYCLOAK_ADMIN=admin -e KEYCLOAK_ADMIN_PASSWORD=admin quay.io/keycloak/keycloak:latest start-dev
```

#### Configure Keycloak Realm

1. Access Keycloak Admin Console: `http://localhost:8080`
2. Create a new realm (e.g., `dmrs`)
3. Create a client for the API:
   - Client ID: `dmrs-api`
   - Client Protocol: `openid-connect`
   - Access Type: `confidential`
   - Valid Redirect URIs: `http://localhost:5000/*`
4. Create a client for the frontend:
   - Client ID: `dmrs-blazor`
   - Client Protocol: `openid-connect`
   - Access Type: `public`
   - Valid Redirect URIs: `http://localhost:5001/*`

#### Update API Configuration

Update `appsettings.json` with Keycloak settings:

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/dmrs",
    "Audience": "dmrs-api",
    "ClientId": "dmrs-api",
    "ClientSecret": "your-client-secret"
  }
}
```

### 4. Run the Application

#### Start the API

```bash
cd DMRS.API
dotnet run
```

The API will be available at: `https://localhost:5000` (or the port specified in launchSettings.json)

#### Start the Blazor Frontend

```bash
cd DMRS.Blazor
dotnet run
```

The frontend will be available at: `https://localhost:5001` (or the port specified in launchSettings.json)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Authors

- **Khair** - _Initial work_ - [Khair69](https://github.com/Khair69)

## Acknowledgments

- [ASP.NET Core in Action](https://www.manning.com/books/asp-net-core-in-action-third-edition) by Andrew Lock
- [HL7 FHIR](https://www.hl7.org/fhir/) for the healthcare interoperability standard
- [Firely SDK](https://fire.ly/products/firely-net-sdk/) for the .NET FHIR implementation
- [Keycloak](https://www.keycloak.org/) for identity and access management
- The ASP.NET Core team for the excellent framework

---

**Note**: This is a healthcare application. Ensure compliance with relevant healthcare regulations (HIPAA, GDPR, etc.) before deploying to production.
