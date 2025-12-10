# Тут будет крутая дока?

> Описание появится совсем скоро.
> По крайней мере, в масштабах космоса — мгновенно.
> А пока наслаждайтесь пустотой и воображайте идеальную документацию.

## 🎥 В фильме снимались...

<table>
  <tr>
    <td align="center">
      <img src="https://media1.tenor.com/m/GOj9ZF_-ZOcAAAAd/cat.gif"
           width="220" height="220"
           style="border-radius: 10px; border: 2px solid #fff; object-fit: cover;">
      <br>
      <a href="https://github.com/YanKarpov"><b>Ян Карпов</b></a>
    </td>
    <td align="center">
      <img src="https://media1.tenor.com/m/DimzPZMypFcAAAAd/laptop.gif"
           width="220" height="220"
           style="border-radius: 10px; border: 2px solid #fff; object-fit: cover;">
      <br>
      <a href="https://github.com/FreseFeaa"><b>Фёдор Палехов</b></a>
    </td>
    <td align="center">
      <img src="https://media1.tenor.com/m/XPRG-4ujVMIAAAAd/cat-work-in-progress.gif"
           width="220" height="220"
           style="border-radius: 10px; border: 2px solid #fff; object-fit: cover;">
      <br>
      <a href="https://github.com/Aniwylle"><b>Диана Пелёвина</b></a>
    </td>
  </tr>
</table>

## Технологический стек / Подход к архитектуре

- .NET 10
- C# 12
- WPF (Windows Presentation Foundation)
- MVVM (частично, для структуры UI)

## Cтруктура проекта
```
├─ StormBase/                 <- Общая библиотека (классы и логика)
│  ├─ Models/                
│  └─ Services/               
├─ StormBase.Tests/               <- Проект с тестами xUnit
│  ├─ Data/                       
│  ├─ DataTests.cs
│  ├─ IntegrationTests.cs
│  ├─ RouteGraphTests.cs
│  ├─ StormProviderTests.cs
│  ├─ StormRouterTests.cs
│  └─ StormBase.Tests.csproj
├─ StormRouterVisualization/  <- WPF UI проект
│  ├─ Services/             
│  ├─ Utilities/            
│  ├─ App.xaml               
│  ├─ MainWindow.xaml         
│  └─ StormRouterVisualization.csproj
├─ StormRouterConsole/      
│  └─ Program.cs             
├─ StormRouter.sln
```
> Console версия в основном служит для сценария на 100 узлов, т.к визуалиция выглядит перегруженной подобной вещью.

## Запуск проекта
Если установлен NET (10 версия, ибо проект на ней)
Просто в терминале любой из папок что запустить одну из версий (Visualiztion or Console)
```
dotnet run
```

Тесты запускать можно прямо из корня

```
dotnet test
```
