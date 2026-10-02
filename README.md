# dotnet-doctor

A .NET global tool that currently displays its version.

## Install

```sh
dotnet tool install --global cap.dotnetdoctor
```

## Use

```sh
dotnet doctor
dotnet doctor --version
```

Both commands print the installed tool version.

## Build the NuGet package

```sh
dotnet pack src/DotnetDoctor/DotnetDoctor.csproj --configuration Release
```
