# AppCAT-Desktop

**AppCAT** is an application compatibility assessment tool inspired by Microsoft's **Application Compatibility Assessment Tool (AppCAT)**.

It is designed to help assess **.NET and Java applications** for potential compatibility issues, modernization requirements, and areas that may require attention when moving applications to newer platforms or runtimes.

> This is an independent project inspired by the concepts and capabilities of Microsoft's AppCAT. It is not an official Microsoft product.

## 🚀 Overview

Modernizing an existing application often requires understanding how the application's source code, APIs, dependencies, and frameworks will behave on a newer runtime or platform.

AppCAT aims to simplify this assessment by analyzing applications and identifying potential areas that may require changes before modernization.

### Supported Application Technologies

* **.NET**
* **Java**

### Key Goals

* 🔍 Analyze application source code
* ⚠️ Identify potential compatibility issues
* 📋 Highlight areas requiring modernization
* 🔗 Identify APIs, dependencies, and patterns that may require changes
* 🚀 Help developers prepare applications for modernization
* 📊 Provide actionable assessment information

## 🏗️ Assessment Workflow

```text
             Application
                  │
          ┌───────┴───────┐
          │               │
        .NET             Java
          │               │
          └───────┬───────┘
                  │
                  ▼
          Source Code Analysis
                  │
                  ▼
        Compatibility Assessment
                  │
                  ▼
       ┌──────────┼──────────┐
       │          │          │
     APIs     Dependencies  Patterns
       │          │          │
       └──────────┼──────────┘
                  │
                  ▼
          Assessment Findings
                  │
                  ▼
       Modernization Insights
```

## ✨ Features

### Application Assessment

Analyze applications to identify potential compatibility and modernization concerns.

### .NET Assessment

Assess .NET applications for potential compatibility issues when upgrading or modernizing the application.

### Java Assessment

Assess Java applications for compatibility considerations, APIs, dependencies, and modernization requirements.

### Compatibility Findings

Surface areas of the application that may require investigation or modification.

### Modernization Insights

Provide developers with information that can help them understand the effort involved in application modernization.

## 🛠️ Technology Stack

### Application

* C#
* .NET 8
* WPF
* Windows Desktop

### Supporting Libraries

* Ookii.Dialogs.Wpf

### Development

* Visual Studio
* Git
* GitHub

## 📋 Use Cases

AppCAT can be used as part of an application modernization assessment for:

* .NET version upgrades
* Java version upgrades
* Legacy application modernization
* Application migration planning
* Compatibility assessment
* API compatibility analysis
* Dependency assessment
* Identifying modernization blockers
* Understanding potential migration effort

## 🚀 Getting Started

### Prerequisites

For building the current application:

* Windows
* .NET 8 SDK
* Visual Studio 2022 or later
* Desktop development with .NET workload

### Clone the Repository

```bash
git clone https://github.com/heyashishsaini/AppCAT.git
cd AppCAT
```

### Build

```bash
dotnet restore
dotnet build
```

### Run

Open the solution in Visual Studio:

```text
AppCAT.sln
```

Then press **F5** to build and run the application.

Or run it using the .NET CLI:

```bash
dotnet run --project AppCAT/AppCAT.csproj
```

## 📚 Reference

This project is inspired by Microsoft's Application Compatibility Assessment Tool (AppCAT), which is part of Azure Migrate's application modernization capabilities.

* [Microsoft AppCAT Documentation](https://learn.microsoft.com/en-us/azure/migrate/appcat/?view=migrate)
* [Azure Migrate](https://azure.microsoft.com/products/azure-migrate/)

## 🤝 Contributing

Contributions, ideas, and suggestions are welcome.

1. Fork the repository.
2. Create a feature branch:

```bash
git checkout -b feature/your-feature
```

3. Make your changes.
4. Commit your changes:

```bash
git commit -m "Add your feature"
```

5. Push your branch:

```bash
git push origin feature/your-feature
```

6. Open a Pull Request.

## 📄 License

This project is an independent implementation inspired by Microsoft's AppCAT.

It is **not an official Microsoft product** and is not affiliated with or endorsed by Microsoft.

See the repository's license for licensing information.

## 👨‍💻 Author

**Ashish Kumar**

GitHub: [@heyashishsaini](https://github.com/heyashishsaini)
