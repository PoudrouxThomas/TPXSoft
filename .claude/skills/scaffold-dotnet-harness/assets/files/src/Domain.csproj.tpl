<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>__NAME__.Domain</RootNamespace>
    <AssemblyName>__NAME__.Domain</AssemblyName>
  </PropertyGroup>

  <!--
    No package references, on purpose. The architecture test asserts this project
    depends on neither EF Core nor ASP.NET, and the cheapest way to keep that true is
    to have nothing here to depend on.
  -->

</Project>
