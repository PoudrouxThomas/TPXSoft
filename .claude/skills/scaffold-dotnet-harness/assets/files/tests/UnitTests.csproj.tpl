<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>__NAME__.UnitTests</RootNamespace>
    <AssemblyName>__NAME__.UnitTests</AssemblyName>
    <IsPackable>false</IsPackable>
    <!--
      Test names are the behavioural specification. Login_WithExpiredToken_Returns401
      says what the system does and fails the moment it stops being true. CA1707 wants
      those underscores gone, which would cost the only executable spec in the repo, so
      it is off in test projects and nowhere else.
    -->
    <NoWarn>$(NoWarn);CA1707;CA1711;CA1861</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/__NAME__.Domain/__NAME__.Domain.csproj" />
    <ProjectReference Include="../../src/__NAME__.Infrastructure/__NAME__.Infrastructure.csproj" />
    <ProjectReference Include="../../src/__NAME__.Api/__NAME__.Api.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="NetArchTest.Rules" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
