# The **WebDAV Emulator** for RU-clouds: Cloud.Mail.Ru & Disk.Yandex.Ru

---

<a href="https://github.com/ZZZConsulting/WebDavMailRuCloud/releases/latest"><img src="https://img.shields.io/github/v/release/ZZZConsulting/WebDavMailRuCloud?include_prereleases"></a>
<img src="https://img.shields.io/github/last-commit/ZZZConsulting/WebDavMailRuCloud" target="_blank"> <img src="https://img.shields.io/github/downloads/ZZZConsulting/WebDavMailRuCloud/total" align="right" target="_blank">


This is the <a href="https://github.com/ZZZConsulting/WebDavMailRuCloud">ZZZ-fork</a> by <img src="https://avatars.githubusercontent.com/u/121279800?v=4&size=16" height="16pt" width="16pt"/> ZZZConsulting.

The original project by <img src="https://avatars.githubusercontent.com/u/5150160?s=48&v=4" height="15 pt" width="15 pt"/>YaR229
resides here: <a href="https://github.com/yar229/WebDavMailRuCloud">https://github.com/yar229/WebDavMailRuCloud/</a>

*К сожалению, оригинальный проект автором слегка подзаброшен и пребывает в подвешенном состоянии.*
*С большими надеждами ждем возвращения оригинального проекта к жизни!*
*Ну, а пока этого не случилось,.. продолжаем попытки поддержать проект на плаву.*

---

# Multilanguage README

[![en](https://img.shields.io/badge/lang-en-red.svg)](./readme.en.md)
[![ru](https://img.shields.io/badge/lang-ru-green.svg)](./readme.md)

---

### Requirements <img src="https://habrastorage.org/files/72e/83b/159/72e83b159c2446b9adcdaa03b9bb5c55.png" width=200 align="right"/>
* [Windows](#windows) - [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework) / [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) / [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
* [Linux](#linux) - [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) / [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
* [OS X](#mac-os-x) - [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) / [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

---

## Изменения

- По ощущениям Яндекс полностью закончил переход на новый API, старые методы эмуляции браузера по работе с файлами
работать перестали. Пришло время полной заменены методов работы с серверами Яндекс.Диска под новый API.

- Как всегда, новая версия выпускается 'чем быстрее, тем лучше', что означает, что тестирование было минимальным
и только основных функций - прочитать и записать файл, создать и удалить папку.
Перед тем, как работать со своими данными с новой версией, **настоятельно рекомендуется** проверить на своей
учетной записи свой обычный набор операций - какие-то Ваши операции с файлами могут не входить
в стандартный тестируемый набор, или иметь отличия в зависимости от учетной записи
(Яндекс раскатывает изменения по учетным записям постепенно, а не всем разом).

- Устаревшие версии для Mono, .NET Framework, .NET Core и .NET выведены из поддержки,
т.к. потребность в них не понятна, а поддержка требуется затрат.
При необходимости (реальной) поддержку можно вернуть - создавайте Issue
и, пожалуйста, напишите хотя бы минимальное *зачем* - ибо интересно же где такая
потребность возникает без возможности перехода на более новую версию.

- При работе с Яндекс.Диском с аутентификацией через `BrowserAuthenticator` полученный куки
кешировался на диске в папке (папка задавалась атрибутом `CacheDir` тэга `BrowserAuthenticator` конфигурационного файла).
В новой версии Эмулятора кеш на диск не пишется, вместо этого сохраняется в памяти на 6 часов и теряется при перезапуске программы.
Если папка под кеш файлов с куки задавалась в конфигурационном файле и использовалась, настоятельно рекомендуется
удалить эту папку вместе с содержимым.

- Для работы с Яндекс.Диском было два набора (репозитория) методов - сохранялся оригинальный набор от Yar229,
а новые методы формировали новый набор. Поскольку методы исходного набора работать перестали, они были полностью
замещены новым набором - с новой версии для Яндекса только один протокол или набор API.
Следствием объединения стало изменение отслеживания завершения операций.
Исходный набор не ожидал завершение обработки операции сервером, а делал это только при следующем обращении,
методы нового набора всегда ожидает завершения, и только потом позволяют продолжить.
Так же штатным стало отслеживание изменений на Яндекс.Диске, сделанных минуя Эмулятор, чего не было в исходном наборе.
Если регулярный мониторинг не требуется, установка параметра `detect-activity-interval` в `0`
полностью отключает регулярное обращение к серверу и отслеживание изменений.

- Не исправлялись не проверялись функции работы с media - фото, альбомы, ссылки на файлы, шифрование и многое-много другое,
что не входит в минимальный базовый набор операций с файлами и папками, пожалуйста, имейте это в виду.

- Удалено параллельное чтение в несколько соединений папок с большим количеством файлов.
При переходе к новому API вылезли некоторые проблемы, для ускорения решения которых были принесены жертвы.
Одновременно с этим, более разумным кажется не чтение в несколько потоков с сервера,
а ускорение разбора возвращаемого сервером xml.

- Все используемые сборки обновлены до последних версий (чего не было крайне давно).
Есть вероятность, что на каких-то машинах новая версия может сбоить как раз по причине версий сборок.
Особенно это актуально для не Windows систем - Linux и MacOS, если такие пользователи здесь еще остались.

- Ну, и с началом весны нас всех!



#### Пароль для Disk.Yandex.Ru

При обращении к Disk.Yandex.Ru через эмулятор WEBDAV, всегда следует указывать основной пароль учетной записи.
Не работает, если использовать пароль, созданный в `Пароли приложений`.

#### Пароль для Cloud.Mail.Ru

При обращении к Cloud.Mail.Ru через эмулятор WEBDAV работают пароли, созданные в `Пароли для внешних приложений`.
Рекомендуется использовать такой пароль.

---

### Это ВАЖНО!

Не смотря на то, что лицензия все обговаривает (да кто ж её читает?),
необходимо напомнить, что за ВАШИ данные несете ответственность только ВЫ!
Программное обеспечение может содержать ошибки. И даже в случае, когда оно прошло самое лучше тестирование, которое может быть, конкретно в Вашей среде, с Вашими настройками, Вашими параметрами, серверами, задержками, файлами и чем угодно еще, программное обеспечение может дать сбой и повести себя не так, как того от него ожидали.
Поэтому, каждый раз, перед тем, как начать использовать новую версию программы на важных для Вас данных, проверьте, что конкретно у Вас и в Вашей среде эта новая версия работает корректно и без критических ошибок, что как минимум она не портит Ваши данные.
Авторы данного программного обеспечения не гарантируют правильность его работы, не гарантируют правильность и сохранность данных,
и не несут ответственность за последствия применения данного программного обеспечения.
Используя данное программное обеспечение, ВЫ берете на себя ответственность за сохранность Ваших данных.
Никто специальным вредительством не занимается, но ошибки в ПО есть всегда!
Тем более, что используется неофициальные API, от чего правильное функционирование программы может прекратиться в любой момент.

### **ВАЖНОЕ** по части облака Яндекса (disk.yandex.ru)!

`Выбор в настройках учетной записи опцией входа «обычный пароль»` и
`Требование пройти дополнительную проверку при входе` (с кодом через СМС или email) не связаны. Это две разные сущности.

В случае, когда пользователь выбирает вход «по паролю и СМС», подтверждение кодом будет при каждом входе. А когда «обычный пароль», то в случаях, когда проверка спровоцирована.

Проверку может «спровоцировать» `полный выход из аккаунта`, использование `разных браузеров`, `разных устройств`, `очистка cookie`, использование режима `«Инкогнито»` и `VPN`.

Если используете облако Яндекса с логином и паролем, попытка использования другой версии эмулятора может привести к невозможности дальнейшего входа только по логину и паролю!

---

#### MRClient.exe

```
  -p, --port            (По умолчанию: 801) Порт (или список портов через `,`),
                        на которых эмулятор WebDAV принимает подключения.
  -h, --host            (По умолчанию: "http://127.0.0.1") адрес и протокол для приема
                        входящих подключений к эмулятору WebDAV (http://* для http://0.0.0.0).

  --proxy-address <socks|https|http>://<address>:<port>   Установка прокси-сервера
  --proxy-user <username>                                 Установка user name для прокси-сервера
  --proxy-password <password>                             Установка password для прокси-сервера

  --maxthreads          (По умолчанию: 5) Максимальное количество одновременно
                        обрабатываемых подключений к эмулятору WebDAV.
  --maxconnections      (По умолчанию: 10) Максимальное количество соединений
                        каждого экземпляра эмулятора WebDAV к облачным серверам.
  --use-locks           (По умолчанию: false) Использовать блокировки
                        одновременного доступа к файлам.
  --cache-listing       (По умолчанию: 30) Таймаут в секундах хранения в памяти
                        списков файлов папок облачных серверов.
                        0 для выключения кеширования списков файлов.
  --cache-listing-shared
                        (По умолчанию: равен значению, заданному параметром --cache-listing)
                        Таймаут в секундах хранения в памяти
                        списков общих файлов и общих папок облачных серверов.
                        0 для выключения кеширования списков общих файлов и общих папок.
  --cache-listing-depth (По умолчанию: 1) Сколько уровней вложенности папок
                        за раз читать с сервера. Всегда равно 1, если cache-listing > 0.
                        Для максимизации производительности задать
                        cache-listing-depth = 1 и cache-listing от 600 до 1800.
  --use-deduplicate     (По умолчанию: false) Включить deduplication
                        (ускорение загрузки по хешу), см. раздел deduplication.
  --disable-links       (По умолчанию: false) Отключить поддержку общих папок и ссылок,
                        хранимых в файлах item.links.wdmrc.
  --detect-activity-interval
                        (По умолчанию: 15) Интервал в секунда, как часто проверять изменения
                        на Яндекс.Диске, сделанные не через Эмулятор, чтобы сбросить кеш,
                        0 для отключения регулярной проверки.
                        Диапазон допустимых значений - от 4 до 60 секунд.

  --protocol            (По умолчанию: Autodetect) Протокол работы с облаком
                        * Autodetect - см. раздел Auto-detect protocol
                        * WebM1Bin   - (Cloud.Mail.Ru) гибрид для мобильных и DiskO
                        * WebV2      - (Cloud.Mail.Ru) [устарел] протокол для браузера на ПК
                        * YadWeb     - (Disk.Yandex.Ru) протокол браузера на ПК,
                                       см. раздел Disk.Yandex.Ru README

  --install <service name>          Установка сервисом Windows
                                    (только для сборок для Windows версий .Net 4.8/7.0/8.0).
  --install-display <display name>  Отображаемое имя для сервиса
                                    (только для сборок для Windows версий .Net 4.8/7.0/8.0).
  --uninstall <service name>        Удаление сервиса из Windows
                                    (только для сборок для Windows версий .Net 4.8/7.0/8.0).

  --100-continue-timeout-sec  (По умолчанию: 1) Таймаут в секундах
                              на получение 100-Continue от сервера при блочной передаче.
  --response-timeout-sec      (По умолчанию: 100) Таймаут в секундах
                              на получение 1-го байта ответа от сервера.
  --read-write-timeout-sec    (По умолчанию: 300) Таймаут в секундах
                              на получение последнего байта данных от сервера.
                              Увеличьте значение при медленном интернете
                              или при загрузке/скачивании больших файлов.
  --cloud-instance-timeout    (По умолчанию: 30) Таймаут в минутах
                              на прекращение работы экземпляра (по облаку и логину) сервиса.
                              Эмулятор WebDAV на каждую пару облако+логин создает свой экземпляр сервиса.
                              При отсутствии обращений к экземпляру сервиса в течение указанного периода,
                              память освобождается от экземпляра сервиса.

  --help                Справка о параметрах на английском языке.
  --version             Версия программы.

  -user-agent           Переопределяет стандартный 'user-agent' в заголовках обращений к облачным серверам.
  -sec-ch-ua            Переопределяет стандартный заголовок 'sec-ch-ua' в обращениях к облачным серверам.
```

---

#### Hasher.exe

Calculating hashes for local files

```
  --files            (Group: sources) Filename(s)/wildcard(s) separated by space

  --lists            (Group: sources) Text files with wildcards/filenames separated by space

  --protocol         (Default: WebM1Bin) Cloud protocol to determine hasher

  -r, --recursive    (Default: false) Perform recursive directories scan

  --help             Display this help screen.

  --version          Display version information.
```

---

### Использование deduplication (ускорение загрузки по хешу вместо содержимого)

Настроить раздел `<Deduplicate>` в `wdmrc.config`:

```
  <Deduplicate>
    <!-- Path for disk file cache -->
    <Disk Path = "d:\Temp\WDMRC_Cache" />

    <!--
      Cache: on disk or in-memory file caching
      Target:  path with filename in cloud, .NET regular expression,
               see https://docs.microsoft.com/ru-ru/dotnet/standard/base-types/regular-expressions
      MinSize: minimum file size
      MaxSize: maximum file size
      -->
    <Rules>
      <!-- cache any path/file contains "EUREKA" in disk cache-->
      <Rule Cache="Disk" Target = "EUREKA" MinSize = "0" MaxSize = "0" />

      <!-- small files less than 15000000 bytes will be cached in memory -->
      <Rule Cache="Memory" Target = "" MinSize = "0" MaxSize = "15000000" />

      <!-- files larger than 15000000 bytes will be cached on disk -->
      <Rule Cache="Disk" Target = "" MinSize = "15000000" MaxSize = "0" />
    </Rules>
  </Deduplicate>
```
Затем запустить с параметром `--use-deduplicate` в командной строке.

---

### Cloud protocol and `Autodetect`

Использование WebDAV с облаками Cloud.Mail.Ru и Disk.Yandex.Ru не лишено проблем.
Эмулятор WebDAV создан чтобы решить эти проблемы с использованием неофициального APIs.
Подмножество методов API для работы с облаком называется протоколом.
Когда пользователь использует эмулятор WebDAV, пытаясь подключиться к облаку,
эмулятору WebDAV необходимо решить:
* к какому облаку будет подключение (Cloud.Mail.Ru или Disk.Yandex.ru),
* какой протокол (API) следует использовать,
* какой тип аутентификации должен быть использован (просто login+password или аутентификация через браузер).

***Autodetect***. Как это работает

Шаг **1**. Определение облака

Если пользователь задал `login` в формате email (например, John@yandex.ru or John@mail.ru)
облако определяется по домену из email (часть после символа `@`).
В остальных случаях (например, `login` задан как John) облако определяется протоколом из параметра `protocol` при запуске приложения.

Шаг **2**. Определение протокола

Если облако уже определено, а протокол еще нет, эмулятор WebDAV использует
* протокол WebM1Bin для Cloud.Mail.Ru и
* протокол YadWeb для Disk.Yandex.Ru.

Шаг **3**. Тип аутентификации

Протокол WebM1Bin имеет только один тип аутентификации - по `login` и `password`.

Для протокола YadWeb для облака Disk.Yandex.Ru есть два варианта:

* по `login` и `password`
и
* аутентификация через специальный браузер - `BrowserAuthenticator`.

По умолчанию YadWeb используется login+password.

Если в конфигурационном задан `BrowserAuthenticator`,
то есть в файле `wdmrc.config` задан тэг `BrowserAuthenticator` и атрибут `password`,
и переданный пользователем пароль совпадает с паролем в конфигурации `BrowserAuthenticator`,
то считается, что требуется аутентификация через браузер.

Для большей надежности пользователь может давать эмулятору WebDAV подсказки, добавляя знаки `!` или `?` в `login` в первую позицию.
`?` в начале означает обязательную аутентификацию через браузер.
`!` в начале означает недопустимость аутентификации через браузер.

Начальные символы `!` и `?` удаляются из логина, передаваемого дальше на облачный сервер.

Если аутентификация через браузер не используется, `password` заполняется основным паролем учетной записи Яндекса.
`Пароли приложений` создаваемые в учетной записи для доступа сторонних приложений здесь не подходят,
т.к. эмулятор WebDAV работает не как другие приложения, а имитирует доступ браузера к облаку.

Если используется аутентификация через браузер, существует два варианта заполнить `password`.
В обоих случаях приложение `BrowserAuthenticator` при всех входящих подключениях сверяет переданный пароль с паролем, заданным в окне настроек приложения `BrowserAuthenticator`.
Вариант первый: передать в эмулятор WebDAV пароль, в точности соответствующий заданному в окне настроек приложения `BrowserAuthenticator`.
Вариант второй: передать в эмулятор WebDAV пустой пароль. Это заставит эмулятор WebDAV при обращении к приложению `BrowserAuthenticator`
использовать пароль, заданный в `wdmrc.config` в тэге `BrowserAuthenticator`.

Если пароль в настройках приложения `BrowserAuthenticator` меняется достаточно часть,
можно положиться на вариант с пустым паролем и задавать его только в одном месте - в конфигурационном файле.

Но если есть риски, что к приложению `BrowserAuthenticator` может подключиться кто-то сторонний,
есть смысл указывать пароль от приложения `BrowserAuthenticator` в каждом подключении к WebDAV emulator,
а в `wdmrc.config` в тэге `BrowserAuthenticator` специально установить неправильный пароль.

---

### Disk.Yandex.Ru

«Косяк» с WebDAV от Disk.Yandex.Ru

* С конца 2019 года при загрузке файлов на Disk.Yandex.Ru по WebDAV были введены ограничения.
* После загрузки файла, сервера Яндекса стали столь долго подсчитывать хеши файлов,
  что общая скорость оказать значительно ниже приемлемого уровня.
  Например, после загрузки 10 ГБ расчет хеша может занять ~1-2 минуты,
  из-за чего большинство клиентов отваливаются по таймауту.
* При этом сам Яндекс заявляет, что в WebDAV все хорошо, их же приложения с WebDAV прекрасно работают,
  а за работу чужих приложений с их WebDAV они не отвечают.

При всех `тормозах` с WebDAV Яндекс.Диск очень даже быстро работает через браузер.
В качестве обходного пути эмулятор WebDAV использует неофициальное Web API, прикидываясь браузером.
Проблема решается в 2 шага:

**1**) Аутентификация на Disk.Yandex.Ru

Чтобы подключиться к Disk.Yandex.Ru нужно аутентифицироваться любым из двух способов:

* Только `login` + `password`.
  В разделе безопасности учетной записи должен быть настроен вход только по логину и паролю.
  `Пароли приложений`, которые позволяет создавать Яндекс для доступа сторонних приложений,
  не должны применяется. Эмулятор WebDAV работает не как другие сторонние приложения,
  он прикидывается браузером, а потому для него не подходят пароли приложений.
  Заполнять `password` нужно только основным паролем учетной записи!
  Заполнять `login` необходимо полным email (например, John@yandex.ru, не кратким John),
  во избежание проблем при определении облака и т.д.

* Стандартная аутентификация в специальном созданном браузере `BrowserAuthenticator`.
  Приложение BrowserAuthenticator создано так, чтобы запускаться при старте Windows.
  При запуске приложение скрывается в области системных иконок чтобы не мешаться.
  Будучи запущенным, приложение `BrowserAuthenticator` ожидает входящих подключений от эмулятора WebDAV,
  а получив вызов открывает окно браузера. позволяя войти в учетную запись облака.
  Если вход успешно состоялся, информация со страницы и куки браузера,
  содержащие информацию об аутентификации, приложением `BrowserAuthenticator` передаются обратно в эмулятор WebDAV.
  Информации, собираемой приложением `BrowserAuthenticator` и передаваемой эмулятору WebDAV
  достаточно чтобы без участия пользователя и от его имени сделать что угодно на облачном сервере!
  По этой причине охраняйте доступ к папкам, где расположена программа `BrowserAuthenticator` и кеш эмулятора WebDAV!
  Подробности читайте в разделе `BrowserAuthenticator`.

**2**) Дисковые операции с Disk.Yandex.Ru

После аутентификации все операции по чтению, записи, созданию и удалению доступны для использования.
И т.к. эмулятор WebDAV имитирует работу пользователя в браузере на облачном сервере, все работает достаточно шустро.
При этом следует помнить, что применяется неофициальное API, из-за чего корректная работа может прекратиться в любой момент.

---

### BrowserAuthenticator

-- это специальный браузер, предназначенный к запуску вместе с Windows,
чья иконка располагается в системной области внизу экрана среди иконок массы других запущенный программ.

BrowserAuthenticator ожидает запросов на аутентификацию от эмулятора WebDAV, а получив такой,
открывает окно браузера и ждет, когда пользователь войдет в нужную облачную учетную запись.
Используя браузер, пользователь может войти в учетную запись, у которой могут быть установлены любые настройки входа:
только `login` и `password`,
`login` + `password` + код из СМС,
или даже вход по even QR-коду или ключу.

Как только вход в нужную учетную запись состоялся, `BrowserAuthenticator` собирает со страницы необходимые данные,
добавляет к ним куки с информацией об аутентификации,
и отправляет обратно в эмулятор WebDAV, который используя полученную информацию а значит,
представляясь вошедшим в учетную запись пользователем, совершает затребованные действия на облачном сервере.

**Помните!**

Позволяя получить ваши данные, Вы даете передаете достаточно сведений, чтобы без Вашего участия
можно было сделать на облачном сервере что угодно от Вашего имени!
Поэтому охраняйте доступ к папкам, где расположено приложение `BrowserAuthenticator`,
а также где расположен эмулятором WebDAV кеш данных, полученных от `BrowserAuthenticator`,
от посторонних!

**Внимательно прочитайте следующую инструкцию по настройке приложения `BrowserAuthenticator`!**

***Установка и настройка BrowserAuthenticator***

1. Сначала выберите место для приложения `BrowserAuthenticator`.
Когда приложение работает, оно создает папки и файлы рядом со своим исполняемым файлом.
По этой причине у приложения должно быть достаточно прав на
создание и удаление папок и файлов внутри выбранной под приложения папке.
Кроме того, в создаваемых папках будет секретная информация, позволяющая получить доступ
к облачным данным пользователей, которые будут аутентифицироваться через `BrowserAuthenticator`,
поэтому к папкам должен быть ограничен доступ.

Одним из лучших будет создание папки где-нибудь в `%userprofile%\AppData\Local`,
например, `%userprofile%\AppData\Local\Applications\BrowserAuthenticator`
(необходимые папки нужно создать вручную).

2. Скачайте пакет BrowserAuthenticator-*-windows.zip, затем распакуйте его содержимое в папку, выбранную под приложение `BrowserAuthenticator`.

3. Нажмите Win+R и запустите `shell:startup`. Поместите ярлык приложения `BrowserAuthenticator` в открывшейся папке автозапуска.
Это позволит приложению `BrowserAuthenticator` запускаться каждый раз при запуске Windows, при входе в учетную запись.
Не делайте копию приложения в папку автозапуска, помещайте туда только ярлык на приложение!
Для первого раза запустите приложение `BrowserAuthenticator` вручную.

4. При запуске приложение `BrowserAuthenticator` помещает свою иконку в системной области внизу экрана среди иконок других приложений.
Приложение остается запущенным до перезагрузки или выхода из учетной записи. Приложение может быть закрыто через меню у иконки приложения в системной области.
Дважды щелкните мышью по иконке <img src="BrowserAuthenticator/files/cloud.ico"/> приложения `BrowserAuthenticator` для открытия окна с Настройками приложения..

5. В окне Настроек приложения задайте порт для входящих соединений и пароль.
Введите email облачной учетной записи и нажмите Test для проверки работоспособности встроенного браузера.
В качестве пароля можно использовать любую не пустую текстовую строку. Для простоты под полем ввода
можно щелкнуть по голубой надписи чтобы создать новый Guid, который и будет новым паролем.

6. Перейдите в папку с установленным эмулятором WebDAV. Откройте `wdmrc.config` и отредактируйте тэг `<BrowserAuthenticator>` (добавьте тэг, если он отсутствует).

Атрибуты тэга `<BrowserAuthenticator>`:

* `Url`="http://`localhost`:`<port>`/" - URL, включающий адрес и порт ПК с запущенным приложением `BrowserAuthenticator`,
  `port` - номер порта, заданный в коне Настроек приложения `BrowserAuthenticator` на предыдущем шаге инструкции.
  `localhost` может быть заменен на любой иной IP, расположенный в любом конце света, главное чтобы к нему имел возможность подключиться эмулятор WebDAV.

* `Password` - пароль, тестовая строка, заданная в качестве пароля в окне Настроек приложения `BrowserAuthenticator` на предыдущем шаге инструкции.
  Этот пароль следует хранить в секрете, т.к. зная пароль этот пароль и email можно получить доступ ко всем данным пользователя в облаке!

* `CacheDir` - полный путь к папке, куда эмулятор WebDAW сохраняет полученную от приложения `BrowserAuthenticator` информацию для доступа к облаку.
  Папка и сохраняемые данные должны храниться в секрете и быть недоступными посторонним, т.к. хранимой в папке информации достаточно для полного доступа к данным в облаке!

На 1-м шаге инструкции создавалась папка для приложения `BrowserAuthenticator` где-то внутри `%userprofile%\AppData\Local`.
Внутри папки с приложением `BrowserAuthenticator` можно создать еще одну под кеш, и указать ее путь в атрибуте `CacheDir`.
Таким образом в безопасности и недоступности нужно будет хранить всего одну папку - ту, где установлено приложение `BrowserAuthenticator`.

В случае, если кеш для эмулятора WebDAW с информацией от приложения `BrowserAuthenticator` не нужен или не желателен,
нужно удалить атрибут `CacheDir` или задать пустую строку вместо пути к папке. 

---

#### Установка сервисом Windows

Установка сервисом Windows (**для пакета dotNet48**).
* Запустить `cmd` в режиме `Запуск от имени администратора`
* Затем ввести, например, `wdmrc.exe --install wdmrc -p 801 --maxthreads 15` <br/>
* `net start wdmrc`

Установка сервисом Windows (**для пакетов dotNet7Win/dotNet8Win**).

* Установка: Запустить `cmd` в режиме `Запуск от имени администратора`,
  затем откорректировать параметры и запустить
  `wdmrc.exe --install WebDavService --maxthreads 10 --maxconnections 20 --port 801 --cache-listing 180`

* Удаление: Запустить `cmd` в режиме `Запуск от имени администратора`,
  затем запустить
  `wdmrc.exe --uninstall WebDavService`

---

### Features

***How to use encryption***

Using XTS AES-256 on-the-fly encryption/decryption

* Set (en/de)cryption password
  * with `>>crypt passwd` special command <br/>
    or
  * Add `#` and separator string to your login: `login@mail.ru#_SEP_`
  * After your mail.ru password add separator string and password for encrypting: `MyLoginPassword_SEP_MyCryptingPassword`

* Mark folder as encrypted using `>>crypt init` command
* After that files uploaded to this folder will be encrypted

***Commands*** <br/>
Commands executed by making directory with special name.<br/>
Parameters with spaces must be screened by quotes.
* `>>join SHARED_FOLDER_LINK` Clone shared cloud.mail.ru file/folder to your account
* `>>join #filehash filesize [/][path]filename` Clone cloud.mail.ru file to your account by known hash and size
* `>>link SHARED_FOLDER_LINK [linkname]` Link shared folder without wasting your space (or manually edit file /item.links.wdmrc)
* `>>link check` Remove all dead links (may take time if there's a lot of links)
* `>>move` `/full/path/from /full/path/to` Fast move (if your client moves inner items recursively)
* `>>copy` `/full/path/from /full/path/to` Fast copy (if your client copies inner items recursively)
* `>>lcopy` `x:/local/path/from /full/server/path/to` If file already in cloud, add it by hash without uploading
* `>>rlist` [[/]path] [list_filename]	list [path] to [list_filename]
* `>>del [[/]path]` Fast delete (if your client makes recursive deletions of inner items)
* `>>share [[/]path]` Make file/folder public <br/>
  - and create `.share.wdmrc` file with links
* `>>sharev [[/]path] [resolution]` Make media file public <br/>
  - `resolution` = `0p` (all), `240p`, `360p`, `480p`, `720p`, `1080p`
  - and create `.share.wdmrc` file with public and direct play links
* `>>pl [[/]path]  [resolution]` Make media file public <br/>
  - `resolution` = `0p` (all), `240p`, `360p`, `480p`, `720p`, `1080p`
  - and create `.share.wdmrc` file with public and direct play links <br/>
  - and create `.m3u8` playlist file
* `>>crypt init` Mark current folder as encrypted
* `>>crypt passwd password_for_encryption_decryption` Set password for encryption/decryption

***Settings*** in `wdmrc.exe.config`
* Logging <br/>
    `<config><log4net>` <br/>
    It's standard [Apache log4net](https://logging.apache.org/log4net/) configurations, take a look for [examples](https://logging.apache.org/log4net/release/config-examples.html)
    Additionally you can use `protocol` and `port` properties taken from command-line parameters.
* Default video resolution for generated m3u playlists
    `<config><DefaultSharedVideoResolution>` <br/>
    Values:
      `0p`      auto, m3u contains links to all available resolutions 
      `240p`    ~ 352 x 240
      `360p`    ~ 480 x 360
      `480p`    ~ 858 x 480
      `720p`    ~ 1280 x 720
      `1080p`   ~ 1920 x 1080
* Default User-Agent <br/>
    `<config><DefaultUserAgent>` <br/>
    Default user-agent for web requests to cloud.
* Special command prefix <br/>
    `<config><AdditionalSpecialCommandPrefix>` <br/>
    custom special command prefix instead of `>>`. Make possible to use special commands if client doesn't allow `>>`.
* Enable/disable WebDAV properties <br/>
    `<config><WebDAVProps>` <br/>
    set `false` on properties you don't need to speedup listing on large catalogs / slow connections.
* 2 Factor Authentication <br/>
    At this time you can use
    * `<TwoFactorAuthHandler Name = "AuthCodeConsole"/>` - asks for authcode in application console
    * `<TwoFactorAuthHandler Name = "AuthCodeWindow"/>` - asks for authcode in GUI window (only for .NET Framework releases)
    * 
        ```
        <TwoFactorAuthHandler Name = "AuthCodeFile">
            <Param Name = "Directory" Value = "d:"/>
            <Param Name = "FilenamePrefix" Value = "wdmrc_2FA_"/>
        </TwoFactorAuthHandler>
        ```
       user must write authcode to file. For example, user `test@mail.ru` writes code to `d:\wdmrc_2FA_test@mail.ru`.
    
    
    Be careful, this methods does not usable when application started as a service/daemon. <br>
    You can make your own 2FA handlers inherited from `ITwoFaHandler` and put it in separate dll which name starts with `MailRuCloudApi.TwoFA`
    
Connect with (almost any) file manager that supports WebDAV using Basic authentication with no encryption and
* your cloud.mail.ru email and password
* or `anonymous` login if only public links list/download required ([WinSCP script example](https://github.com/yar229/WebDavMailRuCloud/issues/146#issuecomment-448978833))

Automatically split/join when uploading/downloading files larger than cloud allows.

[Russian FAQ](https://gist.github.com/yar229/4b702af114503546be1fe221bb098f27) <br/>
[geektimes.ru - Снова про WebDAV и Облако Mail.Ru](https://geektimes.ru/post/285520/) <br/>
[glashkoff.com - Как бесплатно подключить Облако Mail.Ru через WebDAV](https://glashkoff.com/blog/manual/webdav-cloudmailru/) <br/>
[manjaro.ru - Облако Mail.Ru подключаем через эмулятор WebDAV как сетевой диск](https://manjaro.ru/how-to/oblako-mailru-podklyuchaem-cherez-emulyator-webdav-kak-setevoy-disk.html) <br/>


<br/>

<details> 
<summary>Using from Windows Explorer requires enabled Basic Auth for WebDAV</summary>
* Press Win+R, type `regedit`, click OK
* HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WebClient\Parameters
* Right click on the BasicAuthLevel and click Modify
* In the Value data box, type 2, and then click OK.
* Reset computer (or run `cmd` with admin rights and then `net stop webclient`, `net start webclient`)
</details>

<details> 
<summary>Use as Windows disk</summary>
```
net use ^disk^: http://^address^:^port^ ^your_mailru_password^ /USER:^your_mailru_email^
```
</details>

<details>
<summary>Faster WebDAV Performance in Windows 7</summary>
Windows 7 client might perform very bad when connecting to any WebDAV server. This is caused, because it tries to auto-detect any proxy server before any request. Refer to KB2445570 for more information.

* In Internet Explorer, open the Tools menu, then click Internet Options.
* Select the Connections tab.
* Click the LAN Settings button.
* Uncheck the “Automatically detect settings” box.
* Click OK until you’re out of dialog.
</details>

<details>
<summary>By default, Windows limits file size to 5000000 bytes, you can increase it up to 4Gb</summary>
* Press Win+R, type `regedit`, click OK
* HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WebClient\Parameters
* Right click on the FileSizeLimitInBytes and click Modify
* Click on Decimal
* In the Value data box, type 4294967295, and then click OK.
* Reset computer (or run `cmd` with admin rights and then `net stop webclient`, `net start webclient`)
</details>

<details>
<summary>Wrong disk size when mapped as Windows drive</summary>
[Microsoft says - "It's not a bug, it's by design"](https://support.microsoft.com/en-us/kb/2386902)
</details>


#### Linux

(tested under [Elementary OS](https://elementary.io) and [Lubuntu](http://lubuntu.net/))
* download and unzip [latest](https://github.com/ZZZConsulting/WebDavMailRuCloud/releases/latest) release  <sub><sup>([obsolete alternative way](https://toster.ru/q/375448) from [Алексей Немиро](https://toster.ru/user/AlekseyNemiro) )</sup></sub>
* .NET Framework (WebDAVCloudMailRu-*-dotNet48.zip)
  * `sudo apt install apt mono-complete`
  * `mono wdmrc.exe -p <port>`
* .NET  (`WebDAVCloudMailRu-*-dotNet*.zip` but not a `WebDAVCloudMailRu-*-dotNet*Win.zip` version)
  * install [.NET](https://dotnet.microsoft.com/en-us/download#linuxredhat)
  * `dotnet wdmrc.dll <params>`


See also 
* [Package for Gentoo Linux](https://github.com/yar229/WebDavMailRuCloud/issues/66) by [powerman](https://github.com/powerman)
* Docker image by [slothds](https://github.com/slothds) ([DockerHub](https://hub.docker.com/r/slothds/wdmrc-proxy/), [GitHub](https://github.com/slothds/wdmrc-proxy))
* Docker image by [ivang7](https://github.com/ivang7) HTTP & HTTPS [DockerHub](https://hub.docker.com/r/ivang7/webdav-mailru-cloud)




Mount with davfs2
* `mkdir /mnt/<folder>`
* edit `/etc/davfs2/davfs2.conf` set `use_locks       0`
* `sudo mount --rw -t davfs http://<address>:<port> /mnt/<folder>/ -o uid=<current_linux_user>`

As a service (daemon)
* https://github.com/yar229/WebDavMailRuCloud/issues/214


CERTIFICATE_VERIFY_FAILED exception
[Issue 56](https://github.com/yar229/WebDavMailRuCloud/issues/56)
[default installation of Mono doesn’t trust anyone](http://www.mono-project.com/docs/faq/security/)

In short:
```
# cat /etc/ssl/certs/* >ca-bundle.crt
# cert-sync ca-bundle.crt
# rm ca-bundle.crt
```

#### Mac OS X

* download and unzip [latest](https://github.com/ZZZConsulting/WebDavMailRuCloud/releases/latest) release  <sub><sup>([obsolete alternative way](https://toster.ru/q/375448) from [Алексей Немиро](https://toster.ru/user/AlekseyNemiro) )</sup></sub>
* .Net Framework (WebDAVCloudMailRu-*-dotNet45.zip)
  * `brew install mono` (how to install [brew](https://brew.sh/))
  * `mono wdmrc.exe -p <port>`
* .Net Core (WebDAVCloudMailRu-*-dotNetCore20.zip)
  * install [.NET Core](https://www.microsoft.com/net/core#macos)
  * `dotnet wdmrc.dll <params>`

Use any client supports webdav.


#### Remarks
* [**RaiDrive**](https://www.raidrive.com/)
* [**NetDrive**](http://www.netdrive.net/)
* [**rclone mount**](https://rclone.org/)
* [**Total Commander**](http://www.ghisler.com/): 
  - requires to update `WebDAV plugin` to [v.2.9](http://ghisler.fileburst.com/fsplugins/webdav.zip)
  - turn on `(connection properties) -> Send\Receive accents in URLs as UTF-8 Unicode`
* [**WebDrive**](https://southrivertech.com/products/webdrive/): 
  - disable `(disk properties) -> HTTP Settings -> Do chunked upload for large files.`
* [**CarotDAV**](http://rei.to/carotdav_en.html): 
  - check `(connection properties) -> Advanced -> Don't update property.`
* avoid using Unicode non-printing characters such as [right-to-left mark](https://en.wikipedia.org/wiki/Right-to-left_mark) in file/folder names


#### Big thanks
* [Ramon de Klein](https://github.com/ramondeklein) for [nwebdav server](https://github.com/ramondeklein/nwebdav)
* [Erast Korolev](https://github.com/erastmorgan) for [Mail.Ru.net-cloud-client](https://github.com/erastmorgan/Mail.Ru-.net-cloud-client)
* [Gareth Lennox](https://bitbucket.org/garethl/) for [XTSSharp](https://bitbucket.org/garethl/xtssharp)
* [C-A-T](https://github.com/C-A-T9LIFE) for testing and essential information
* [YaR229](https://github.com/yar229) for original [WebDavMailRuCloud](https://github.com/yar229/WebDavMailRuCloud)


#### See also<br>
*  Official client [Disk-O:](https://disk-o.cloud/)
*  [Total Commander plugin for cloud.mail.ru service](https://github.com/pozitronik/CloudMailRu)<br>
*  [MARC-FS - FUSE filesystem attempt for Mail.Ru Cloud](https://gitlab.com/Kanedias/MARC-FS)<br>
