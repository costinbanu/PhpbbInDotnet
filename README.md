# A phpBB revamp in .Net

This is a .Net version of the popular phpBB forums platform, targeting MySql. It currently does not offer 100% of all features, but it is fully compatible with the database and can be just "plugged in" to the DB (after running an automated DB update process).

## Prerequisites
This installation requires a MySQL type of server (and it is fully compatible with MariaDB and AuroraDB as well).

This application requires stored procedures, so you must have the proper setup for creating them (for example, MariaDB requires root access for this).

This application uses [Google reCAPTCHA v3](https://www.google.com/recaptcha/about/) for spam protection. Visit their website and set reCAPTCHA up before deploying the application. The setup is as simple as entering the site keys in the configuration.

This application uses AES symmetric encryption for encrypting the password reset codes. The encryption key is generated by concatenating two guids, which must be specified in the configuration. Use this [online guid generator](https://www.guidgenerator.com/) to get your guids.

## Installation
### Set up the configuration
This application requires a custom configuration object. You may use your own app settings provider (`appsettings.json` file, user secrets, Azure app settings store and so on) to set it up, but be aware that by default it only supports `appsettings.json` (so it will require some coding to add your own provider).
Either way,  ensure that its structure and contents follow the sample below. All fields are detailed below.

```json
{
  "ForumDbConnectionString": "...",
  "Recaptcha": {
    "SiteKey": "...",
    "SecretKey": "...",
    "BaseAddress": "https://www.google.com",
    "RelativeUri": "recaptcha/api/siteverify",
    "ClientName": "g-recaptcha",
    "MinScore": 0.6
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
  "ExternalImageProcessor": {
    "Enabled": false,
    "Name": "...",
    "Url": "...",
    "Api": {
      "Enabled": false,
      "ClientName": "ExternalImageProcessor",
      "BaseAddress": "...",
      "RelativeUri": "api/process-image",
      "ApiKey": "..."
    }
  },
  "InternetSearchUrlFormat": "https://www.google.com/search?q={0}",
  "IpWhoIsUrlFormat": "https://whatismyipaddress.com/ip/{0}"
}
```

**Field details**

Field name | Data type | Value | Notes
--- | --- | --- | ---
ForumDbConnectionString | string | ... |  your DB connection string (root access, no implicit database selected)
Recaptcha.SiteKey | string | ... | site key 
Recaptcha.SecretKey | string | ... | secret key 
Recaptcha.BaseAddress | string | https://www.google.com | Base URL for captcha verification 
Recaptcha.RelativeUri | string | recaptcha/api/siteverify | Relative URL for captcha verification 
Recaptcha.ClientName | string | g-recaptcha | HttpClientName used in dependency injection 
Recaptcha.MinScore | decimal | 0.6 | this value can be changed as per [reCAPTCHA documentation](https://developers.google.com/recaptcha/docs/v3); 0.6 is the value working best for our setup
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
Storage.Files | string | forumfiles | path relative to `wwwroot` 
Storage.Avatars | string | forumfiles/avatars | path relative to `wwwroot` 
Storage.Emojis | string | images/smilies | path relative to `wwwroot` 
AvatarMaxSize.Width | int | 200 | pixels
AvatarMaxSize.Height | int | 200 | pixels
EmojiMaxSize.Width | int | 100 | pixels
EmojiMaxSize.Height | int | 100 | pixels
DisplayExternalLinksMenu | bool | false | whether a menu with external links is displayed below the header, next to the forum Menu. 
UseHeaderImage | bool | false | whether a custom image is displayed in the header, instead of the forum name
RecycleBinRetentionTime | TimeSpan | 7.00:00:00 | for how long are deleted items kept in the recycle bin. Is read as `TimeSpan` (format `dd.HH:mm:ss`), default value is 7 days. A value less than one day will trigger an error and will not delete anything at all.
OperationLogsRetentionTime | TimeSpan | 365.00:00:00 | Anything that alters post, topic, forum or user state is saved as an operation log and can be viewed in the forum's admin panel. This value controls for how long are the operation log items kept in the database. Is read as `TimeSpan` (format `dd.HH:mm:ss`), default value is 365 days. An explicit zero value (`0.00:00:00`) can be used for retaining logs indefinitely. A value less than one day will trigger an error and will not delete anything at all.
ExternalImageProcessor.Enabled | bool | ... | Whether a link to an external image processing tool is displayed in the posting page or not
ExternalImageProcessor.Name | string | ... | Name of the external image processing tool that is displayed in the posting page
ExternalImageProcessor.Url | string | ... | Link to the external image processing tool that is displayed in the posting page
ExternalImageProcessor.Api.Enabled | bool | ... | Whether the attached files upload logic integrates with the [Simple Image Processor Project](https://github.com/costinbanu/SimpleImageProcessor) or not
ExternalImageProcessor.Api.ClientName | string | ExternalImageProcessor | Image Processor API `HttpClient` name
ExternalImageProcessor.Api.BaseAddress | string | ... | Image Processor API base address
ExternalImageProcessor.Api.RelativeUri | string | api/process-image | Image Processor API image processing route
ExternalImageProcessor.Api.ApiKey | string | ... | Image Processor API Api Key
InternetSearchUrlFormat | string | https://www.google.com/search?q={0} | Internet search link; query parameter should be URL-escaped
IpWhoIsUrlFormat | string | https://whatismyipaddress.com/ip/{0} | IP WHOIS link

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

### Install the application

1. Back up your installation and database (even though the installation performs only additive operatrions in the database, without altering or deleting existing structures or data, please do it anyway)
2. Build the solution and deploy it to your host.
3. Execute the `PhpbbInDotnet.Database.SetupApp` application (make sure its `appsettings.json` file contains a valid connection string targeting your MySQL / MariaDB / AuroraDB server with root access)
4. Done!

## Feature and functionality differences
As of now, the platform **does not support the following phpBB features**:
- Bookmarks
- Forum icons
- Post approval
- Private message folders
- Forum and topic watch
- User warnings

The platform also **supports some new features**:
- Personalized pagination (per user and topic)
- Attachment upload quota (per group)
- Personalized edit time of own posts (per group or user, with the latter taking precedence if both defined)
- Post soft delete and "recycle bin" functionality for moderators and admins (deleted posts are kept for a configurable period of time and can be restored during this period)

## Further reading
### Technical considerations
This application targets .Net 6.0 and is platform agnostic.

However, it has not yet been researched if the database support can be extended further than the MySQL-compatible languages. Given that the code leverages both stored procedures and inline SQL (through [Dapper](https://github.com/DapperLib/Dapper)), as well as LINQ-to-SQL statements (through [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)), it is expected that multiple database support might not be trivial and might take more time to set up.

### phpBB backwards compatibility
- Authentication is backwards compatible with the hashed passwords from an existing database. The [CryptSharp.Core](https://github.com/costinbanu/CryptSharp.Core) library is used to achieve this.
- Text formatting and rendering is backwards-compatible with the original phpBB rendering engine:
the new platform can render 100% of bb code written on a phpBB platform, while a phpBB platform can render around 90% of bb code written on this platform. If you intend to use a phpBB platform in parallel (on the same DB), beware of the [bbcode_uid](https://www.phpbb.com/community/viewtopic.php?p=12406835) field! Our platform does not set it by default, which might cause problems for rendering in phpbb.
However, our BB code rendering library DOES support this field and provides some methods for backwards compatibility (see its documentation, you will have to get your hands dirty for this).
The [CodeKicker.BBCode.Core](https://github.com/costinbanu/CodeKicker.BBCode.Core) library is used for rendering bb code.

### "Bonus" features
- This platform can be connected to an image resize and license plate hiding API and can perform these tasks automatically, for each uploaded image attachment. I have chosen to isolate the image resize and license plate detection and hiding logic in a distinct API because this functionality is built exclusively for Windows, while I intend PhpbbInDotnet to be platform-agnostic. So if you have a Windows host at hand, and wish to offer automatic image resize and license plate hiding functionality for all attached images within your forum installation, look up the [SimpleImageProcessor](https://github.com/costinbanu/SimpleImageProcessor) repo.

### Maintenance and future work
This platform is currently live at https://forum.metrouusor.com/ (which has served as basis for feature implementation, as well as a beta-testing site by running the old phpBB installation and the new platform in parallel for several months). As long as this forum is up, PhpbbInDotnet is expected to be maintained regularily. Established in 2009 and with a member base of over 1800 people as of march 2022, this forum is one of the largest communities for urban mobility, infrastructure and public transportation fans in Romania, so it is expected to be up and running for a long time (:

Although unplanned as of now, future work will include more themes and more languages, as well as other features brought up by active users.