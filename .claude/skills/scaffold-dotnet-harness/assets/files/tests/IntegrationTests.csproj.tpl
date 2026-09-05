<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>__NAME__.IntegrationTests</RootNamespace>
    <AssemblyName>__NAME__.IntegrationTests</AssemblyName>
    <IsPackable>false</IsPackable>
    <!-- Test names are the specification; see the unit test project for why CA1707 is off. -->
    <NoWarn>$(NoWarn);CA1707;CA1711;CA1861</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/__NAME__.Api/__NAME__.Api.csproj" />
    <ProjectReference Include="../../src/__NAME__.Infrastructure/__NAME__.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
