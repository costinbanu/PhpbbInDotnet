# A phpBB revamp in .Net

This is a .Net version of the popular phpBB forums platform, targeting MySql and Sql Server. It is fully compatible with the database and can be just "plugged in" to an existing DB (after running an automated DB update process).

## Prerequisites
This installation requires either a MySQL type of server (and it is fully compatible with MariaDB and AuroraDB as well), or a Sql Server.

This application requires stored procedures, so you must have the proper setup for creating them (for example, MariaDB requires root access for this).

This application uses [CloudFlare turnstile](https://www.cloudflare.com/application-services/products/turnstile/) for spam protection. Visit their website and set your turnstile up before deploying the application. The setup is as simple as entering the site keys in the configuration. Google reCAPTCHA is not compatible out-of-the-box with the current setup, but it can be made compatible with minimal coding.

This application uses AES symmetric encryption for encrypting the password reset codes. The encryption key is generated by concatenating two guids, which must be specified in the configuration. Use this [online guid generator](https://www.guidgenerator.com/) to get your guids.

## Installation
### Set up the configuration
This application requires a custom configuration object. You may use your own app settings provider (`appsettings.json` file, user secrets, Azure app settings store and so on) to set it up, but be aware that by default it only supports `appsettings.json` (so it will require some coding to add your own provider).
Either way,  ensure that its structure and contents follow the sample below. All fields are detailed below.

```json
{
   "Database": {
    "DatabaseType": "...",
    "ConnectionString": "..."
  },
  "BotDetectionOptions": {
    "SiteKey": "...",
    "SecretKey": "...",
    "BaseAddress": "https://challenges.cloudflare.com",
    "RelativeUri": "turnstile/v0/siteverify",
    "ClientName": "botDetectorApi"
  },
  "Smtp": {
    "Host": "...",
    "Username": "...",
    "Password": "...",
    "EnableSsl": ...,
    "Port": ...
  },
  "Encryption": {
    "Key1": "...",
    "Key2": "..."
  },
  "AvatarSalt": "...",
  "BaseUrl": "...",
  "ForumName": "...",
  "LoginSessionSlidingExpiration": "30.00:00:00", 
  "UploadLimitsMB": {
    "Images": 2,
    "OtherFiles": 20
  },
  "UploadLimitsCount": {
    "Images": 10,
    "OtherFiles": 10
  },
  "UserActivityTrackingInterval": "00.01:00:00", 
  "AdminEmail": "...",
  "Storage": { 
    "StorageType": "...", 
    "ConnectionString": "...",
    "ContainerName": "...",
    "Files": "forumfiles",
    "Avatars": "forumfiles/avatars",
    "Emojis": "images/smilies"
  },
  "AvatarMaxSize": {
    "Width": 200,
    "Height": 200
  },
  "EmojiMaxSize": {
    "Width": 100,
    "Height": 100
  },
  "DisplayExternalLinksMenu": false,
  "UseHeaderImage": false,
  "RecycleBinRetentionTime": "7.00:00:00",
  "OperationLogsRetentionTime": "365.00:00:00",
  "InternetSearchUrlFormat": "https://www.google.com/search?q={0}",
  "IpWhoIsUrlFormat": "https://whatismyipaddress.com/ip/{0}",
  "RecurringTasksTimeToRun": "02:00",
  "ForumIsReadOnly": false,
  "MinimumAge": 16,
  "RateLimitBots": false
}
```

**Field details**

Field name | Data type | Value | Notes
--- | --- | --- | ---
Database.DatabaseType | PhpbbInDotnet.Domain.DatabaseType | MySql |  database type (string representation of the enum)
Database.ConnectionString | string | ... |  your DB connection string (root access required for MySql, an implicit database should be selected)
BotDetectionOptions.SiteKey | string | ... | site key 
BotDetectionOptions.SecretKey | string | ... | secret key 
BotDetectionOptions.BaseAddress | string | https://challenges.cloudflare.com | Base URL for spam verification 
BotDetectionOptions.RelativeUri | string | turnstile/v0/siteverify | Relative URL for spam verification 
BotDetectionOptions.ClientName | string | botDetectorApi | HttpClientName used in dependency injection 
Smtp.Host | string | ... | your SSL-enabled SMTP host 
Smtp.Username | string | ... | SMTP username 
Smtp.Password | string | ... | SMTP password
Smtp.EnableSsl | boolean | ... | Whether SSL is enabled for this host
Smtp.Port | int | ... | SMTP port
Encryption.Key1 | Guid | ... | first guid for AES symmetric key generation 
Encryption.Key2 | Guid | ... | second guid for AES symmetric key generation 
AvatarSalt | string | ... | it is a unique way of naming avatar files; the recommended default value is a lowercase guid without dashes. If this is a new installation, then use the guid generator mentioned above to generate a lowercase guid without dashes. However, if this is an update from phpBB, then get your avatar salt by running this in your forum's DB: `SELECT config_value FROM phpbb_config WHERE config_name = 'avatar_salt'`
BaseUrl | string | ... | forum base url
ForumName |string |  ... | forum name 
LoginSessionSlidingExpiration | TimeSpan | 30.00:00:00 | inactivity time before user is logged out. Is read as `TimeSpan` (format `dd.HH:mm:ss`), default value is 30 days
UploadLimitsMB.Images | int | 2 | applies for both internally and externally hosted images
UploadLimitsMB.OtherFiles | int | 20 | applies only for internally hosted attachments
UploadLimitsCount.Images | int | 10 |  applies for both internally and externally hosted images
UploadLimitsCount.OtherFiles | int | 10 | applies only for internally hosted attachments
UserActivityTrackingInterval | TimeSpan | 00.01:00:00 | time interval for tracking same user's activity. Is read as `TimeSpan` (format `dd.HH:mm:ss`), default value is one hour
AdminEmail | string | ... | sender email address for forum generated emails 
Storage.Type | PhpbbInDotnet.Domain.StorageType | HardDisk | storage type (string representation of the enum)
Storage.Container | string | ... | container name
Storage.ConnectionString | string |... | connection string matching the storage type 
Storage.Files | string | forumfiles | path relative to `wwwroot` if storage type is `HardDisk`, or to the container root otherwise
Storage.Avatars | string | forumfiles/avatars | path relative to `wwwroot` if storage type is `HardDisk`, or to the container root otherwise
Storage.Emojis | string | images/smilies | path relative to `wwwroot` if storage type is `HardDisk`, or to the container root otherwise
AvatarMaxSize.Width | int | 200 | pixels
AvatarMaxSize.Height | int | 200 | pixels
EmojiMaxSize.Width | int | 100 | pixels
EmojiMaxSize.Height | int | 100 | pixels
DisplayExternalLinksMenu | bool | false | whether a menu with external links is displayed below the header, next to the forum Menu. 
UseHeaderImage | bool | false | whether a custom image is displayed in the header, instead of the forum name
RecycleBinRetentionTime | TimeSpan | 7.00:00:00 | for how long are deleted items kept in the recycle bin. Is read as `TimeSpan` (format `dd.HH:mm:ss`), default value is 7 days. A value less than one day will trigger an error and will not delete anything at all.
OperationLogsRetentionTime | TimeSpan | 365.00:00:00 | Anything that alters post, topic, forum or user state is saved as an operation log and can be viewed in the forum's admin panel. This value controls for how long are the operation log items kept in the database. Is read as `TimeSpan` (format `dd.HH:mm:ss`), default value is 365 days. An explicit zero value (`0.00:00:00`) can be used for retaining logs indefinitely. A value less than one day will trigger an error and will not delete anything at all.
InternetSearchUrlFormat | string | https://www.google.com/search?q={0} | Internet search link; query parameter should be URL-escaped
IpWhoIsUrlFormat | string | https://whatismyipaddress.com/ip/{0} | IP WHOIS link
RecurringTasksTimeToRun | string | 02:00 | Required recurring tasks (DB table sync etc) will run daily at this specified hour (UTC). Must be in the HH:mm format.
ForumIsReadOnly | bool | false | Whether the entire forum is in read-only mode. This means that the forum can be read, but no posts or private messages can be submitted.
MinimumAge | int | 16 | Minimum age for users to register
RateLimitBots | bool | false | Whether bots should be rate limited (if true, the app will allow at most 50 instances of a bot per user agent within the timespan configured in the UserActivityTrackingInterval setting, while all other instances would receive a 429 Too Many Requests response)
### Branding
#### Forum header
The application will display the `ForumName` app setting value in the upper left corner of the screen (as a header that links to the forum's first page).

The application supports custom header images and will require three versions (depending on the client's width, it will display the appropriate one):
1. **wwwroot/images/forumlogo-full.png** with the exact size 682 x 100 px
2. **wwwroot/images/forumlogo-medium.png** with the exact size 325 x 77 px
3. **wwwroot/images/forumlogo-small.png** with the exact size 128 x 69 px

In order to start using custom headers, set the `UseHeaderImage` app setting to `true`, then provide the three required images.

#### Custom external links
The application can display a custom set of external links next to the menu. If you need to display this, then set the `DisplayExternalLinksMenu` app setting to `true`, then edit the **`ExternalLinks.<lang>.html`** translation file to add your links.

### Distributed cache
This application uses a distributed cache for optimization. If the database engine is MySQL, then the cache is in-memory
(and not really distributed). If the database engine is SQL Server, then the cache is hosted in a SQL Server table.
Run this command to set up the cache table:

`dotnet sql-cache create "<your SQL Server connection string>" dbo distributed_cache`

### Install the application

1. Back up your installation and database (even though the installation performs only additive operatrions in the database, without altering or deleting existing structures or data, please do it anyway)
2. Build the solution and deploy it to your host.
3. Execute the `PhpbbInDotnet.Database.SetupApp` application (make sure its `appsettings.json` file contains a valid connection string targeting your MySQL / MariaDB / AuroraDB server with root access)
4. If you are hosting your database in SQL server, then you need to create the cache table as per the above instructions 
5. Done!

## Feature and functionality differences
As of now, the platform **does not support the following phpBB features**:
- Bookmarks
- Forum icons
- Post approval
- Private message folders
- User warnings

The platform also **supports some new features**:
- Personalized pagination (per user and topic)
- Attachment upload quota (per group)
- Personalized edit time of own posts (per group or user, with the latter taking precedence if both defined)
- Post soft delete and "recycle bin" functionality for moderators and admins (deleted posts are kept for a configurable period of time and can be restored during this period)
- Recurring tasks that run daily and perform
    - Automatic synchronization between tables (ensures the post, topic and forum links are intact)
    - Automatic cleanup of
        - orphan files
        - forum logs (forum actions, not to be confused with the application log files)
        - recycle bin
    - Automatic sitemap generation

## Further reading
### Technical considerations
This application targets .Net 8.0 and is platform agnostic.

However, it has not yet been researched if the database support can be extended further than the MySQL-compatible languages and Sql Server.

### phpBB backwards compatibility
- Authentication is backwards compatible with the hashed passwords from an existing database. The [CryptSharp.Core](https://github.com/costinbanu/CryptSharp.Core) library is used to achieve this.
- Text formatting and rendering is backwards-compatible with the original phpBB rendering engine:
the new platform can render 100% of bb code written on a phpBB platform, while a phpBB platform can render around 90% of bb code written on this platform. If you intend to use a phpBB platform in parallel (on the same DB), beware of the [bbcode_uid](https://www.phpbb.com/community/viewtopic.php?p=12406835) field! Our platform does not set it by default, which might cause problems for rendering in phpbb.
However, our BB code rendering library DOES support this field and provides some methods for backwards compatibility (see its documentation, you will have to get your hands dirty for this).
The [CodeKicker.BBCode.Core](https://github.com/costinbanu/CodeKicker.BBCode.Core) library is used for rendering bb code.

### Maintenance and future work
This platform is currently live at https://forum.metrouusor.com/ (which has served as basis for feature implementation, as well as a beta-testing site by running the old phpBB installation and the new platform in parallel for several months). As long as this forum is up, PhpbbInDotnet is expected to be maintained regularily. Established in 2009 and with a member base of over 1800 people as of march 2022, this forum is one of the largest communities for urban mobility, infrastructure and public transportation fans in Romania, so it is expected to be up and running for a long time (:

This forum is currently hosted in Azure (as a Linux App Service) and targets SqlServer.

Although unplanned as of now, future work will include more themes and more languages, as well as other features brought up by active users.