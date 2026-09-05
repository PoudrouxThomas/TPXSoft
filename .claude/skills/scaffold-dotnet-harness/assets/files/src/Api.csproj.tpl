<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <RootNamespace>__NAME__.Api</RootNamespace>
    <AssemblyName>__NAME__.Api</AssemblyName>
  </PropertyGroup>

  <!--
    The OpenAPI document is emitted by the build, not by a running server, which is what
    makes it reviewable in a diff and generatable in CI without starting anything.
  -->
  <PropertyGroup>
    <OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>
    <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../../artifacts/openapi</OpenApiDocumentsDirectory>
    <OpenApiGenerateDocumentsOptions>--file-name openapi</OpenApiGenerateDocumentsOptions>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../__NAME__.Domain/__NAME__.Domain.csproj" />
    <ProjectReference Include="../__NAME__.Infrastructure/__NAME__.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Microsoft.Extensions.ApiDescription.Server">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

</Project>
