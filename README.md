Позже тут будет супер-дупер-крутое описание...

Типо структура проекта
```
├─ StormBase/                 <- Общая библиотека (классы и логика)
│  ├─ Models/                 <- Классы данных: InputData, Route, Storm, RouteSegment, RouteState
│  └─ Services/               <- Класс StormRouter с алгоритмом маршрутизации
├─ StormRouterVisualization/  <- WPF UI проект
│  ├─ Data/                   <- Примеры данных / JSON
│  ├─ Services/               <- Сервисный код, если нужен только для UI
│  ├─ Utilities/              <- Вспомогательные классы
│  ├─ App.xaml                <- WPF приложение
│  ├─ MainWindow.xaml         <- Главное окно
│  └─ StormRouterVisualization.csproj
├─ StormRouterConsole/        <- Консольное приложение для тестирования и вывода маршрутов
│  └─ Program.cs              <- Консольный запуск, красивый вывод маршрутов
```

Как запускать? Если установлен NET (10 версия, ибо проект на ней)
Просто в терминале любой из папок что запустить одну из версий
```
dotnet run
```

<p align="center">
  <img src="https://media1.giphy.com/media/v1.Y2lkPTc5MGI3NjExa282OW9qZWEzMmUzeDgxOTg3ajAxbXN4bnNlZXdqcmp3amF2ZGk2aSZlcD12MV9pbnRlcm5hbF9naWZfYnlfaWQmY3Q9Zw/KbdF8DCgaoIVC8BHTK/giphy.gif" alt="Cat">
</p>
