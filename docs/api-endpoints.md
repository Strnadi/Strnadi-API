# Strnadi API — Endpoint Catalog

Generated 2026-08-31 from the XML doc comments (`<summary>`/`<param>`/`<returns>`) added to every
controller action. Reference for generating/maintaining the hand-written OpenAPI spec at
`../StrnadiAPI-openapi.yaml` (do not overwrite that file from this one — this is a source, not a mirror).

Conventions used below:
- **Route** = controller `[Route]` + action route template.
- **Params** lists only real API-surface parameters (route/query/body/form). `[FromServices]` DI
  parameters are omitted — they aren't part of the HTTP contract.
- **Auth** is inferred from the method body's checks (JWT presence/validity, admin check, ownership check).
- `[Obsolete]` endpoints are flagged with their replacement.

Total: **84 endpoints** across **10 controllers** (`DictionaryController` is an empty stub, no endpoints).

---

## AchievementsController — `achievements` (3)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| GET | `achievements` | `userId` query int? optional | none | List achievement definitions, or evaluate+return one user's progress if `userId` given | 200 array, 404 if load failed |
| GET | `achievements/{achievementId:int}/photo` | `achievementId` route int | none | Download an achievement's icon PNG | 200 image/png, 404 not found |
| POST | `achievements` | `sql` form string, `contents` form string (JSON array), `file` form file | JWT + admin | Create achievement definition + icon | 200 ok, 400 missing JWT/bad contents, 401 not admin, 409 failure |

---

## ArticlesController — `articles` (21)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| GET | `articles` | — | none | List all published articles | 200 array, 204 none, 500 failure |
| GET | `articles/categories` | `articles` query bool = true | none | List categories, optionally with their articles | 200 array, 204 none, 500 failure |
| GET | `articles/{categoryName}` | `categoryName` route string | none | Get all articles in a category | 200, 500 failure |
| GET | `articles/{id:int}` | `id` route int | none | Get single article | 200, 500 failure |
| GET | `articles/{id:int}/{fileName}` | `id` route int, `fileName` route string | none | Download article attachment file | 200 file, 404 not found |
| POST | `articles` | body `ArticleUploadRequest` | JWT | Create article | 200 id, 400 no JWT, 401 invalid, 500 failure |
| POST | `articles/{id:int}/{fileName}` | `id` route int, `fileName` route string, body string (base64) | JWT | Upload article attachment | 200, 400 no JWT, 401 invalid, 500 failure |
| POST | `articles/categories` | body `ArticleCategoryUploadRequest` | JWT + admin | Create article category | 200, 400 no JWT, 401 not admin, 500 failure |
| PATCH | `articles/{id:int}` | `id` route int, body `ArticleUpdateRequest` | JWT | Update article | 200, 400 no JWT, 401 invalid, 500 failure |
| GET | `articles/translations/{id:int}` | `id` route int | none | Get article translation | 200, 404 not found |
| PATCH | `articles/translations/{id:int}` | `id` route int, body `ArticleTranslationUpdateRequest` | JWT | Update article translation | 200, 400 no JWT, 401 invalid, 500 failure |
| DELETE | `articles/translations/{id:int}` | `id` route int | JWT | Delete article translation | 200, 400 no JWT, 401 invalid, 500 failure |
| GET | `articles/categories/translations/{id:int}` | `id` route int | none | Get category translation | 200, 404 not found |
| PATCH | `articles/categories/translations/{id:int}` | `id` route int, body `ArticleCategoryTranslationUpdateRequest` | JWT | Update category translation | 200, 400 no JWT, 401 invalid, 500 failure |
| DELETE | `articles/categories/translations/{id:int}` | `id` route int | JWT | Delete category translation | 200, 400 no JWT, 401 invalid, 500 failure |
| PATCH | `articles/{id:int}/{fileName}` | `id` route int, `fileName` route string, body string (base64) | JWT + admin | Replace article attachment | 200, 400 no JWT, 401 not admin, 500 failure |
| PATCH | `articles/{categoryName}` | `categoryName` route string, body `AssignArticleToCategoryRequest` | JWT + admin | Assign article to category | 200, 400 no JWT, 401 not admin, 500 failure |
| DELETE | `articles/{id:int}` | `id` route int | JWT | Delete article | 200, 400 no JWT, 401 invalid, 500 failure |
| DELETE | `articles/{id:int}/{fileName}` | `id` route int, `fileName` route string | JWT | Delete article attachment | 200, 400 no JWT, 401 invalid, 500 failure |
| DELETE | `articles/categories/{categoryName}` | `categoryName` route string | JWT + admin | Delete category | 200, 400 no JWT, 401 not admin, 500 failure |
| DELETE | `articles/categories/{categoryName}/{articleId:int}` | `categoryName` route string, `articleId` route int | JWT + admin | Remove article from category | 200, 400 no JWT, 401 not admin, 500 failure |

---

## AuthController — `/auth` (14)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| GET | `auth/verify-jwt` | — | JWT | Check if caller's email is verified | 200 verified, 400 no JWT/email, 403 not verified |
| GET | `auth/renew-jwt` | — | JWT (email from token) | Issue fresh JWT | 200 new JWT, 400 invalid, 409 user gone |
| POST | `auth/sign-up-google` | body `GoogleAuthRequest` | Google ID token | Register via Google | 200 jwt+profile, 401 bad token, 409 exists |
| POST | `auth/login-google` | body `GoogleAuthRequest` | Google ID token | Login via Google | 200 jwt, 401 bad token, 409 not exists |
| POST | `auth/google` | body `GoogleAuthRequest` | Google ID token, optional bearer JWT to link | Login/signup/link via Google | 200 (link/existing/new + jwt), 401 bad token/JWT |
| POST | `auth/apple` | body `AppleAuthRequest` | Apple ID token, optional bearer JWT to link | Login/signup/link via Apple | 200 jwt+profile, 401 bad token/JWT |
| POST | `auth/apple-callback` | form `user?`, `state?`, `id_token?` | none | Apple web sign-in form-post callback → redirect to return URL | redirect, 400 missing state |
| POST | `auth/apple/callback` | form `user?`, `state?`, `id_token?`, `code?` | none | Apple sign-in callback for Android → intent:// deep link redirect | redirect |
| GET | `auth/has-apple-id` | `userId` query int | none | Check if user has Apple linked | 200 linked, 409 not linked |
| GET | `auth/has-google-id` | `userId` query int | none | Check if user has Google linked | 200 linked, 409 not linked |
| POST | `auth/login` | body `LoginRequest` | credentials | Email/password login | 200 jwt, 409 not exist, 401 bad password |
| POST | `auth/sign-up` | body `SignUpRequest` | credentials or bearer JWT (social completion) | Register new user, sends verification email | 200 jwt, 409 exists/create failed |
| GET | `auth/{userId:int}/resend-verify-email` | `userId` route int | JWT matching user | Resend verification email | 200, 401 invalid/mismatch, 208 already verified |
| GET | `auth/{email}/reset-password` | `email` route string | none | Send password reset email | 200, 404 user not found |

---

## DevicesController — `/devices` (3)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| POST | `devices/add` | body `AddDeviceRequest` | JWT | Register device / reassign FCM token to user | 200, 400 no JWT, 401 invalid, 409 user missing/failed |
| PATCH | `devices/update` | body `UpdateDeviceRequest` | JWT | Update device's FCM token | 200, 400 no JWT, 401 invalid, 409 user/device missing, 500 failure |
| DELETE | `devices/delete/{fcmToken}` | `fcmToken` route string | JWT | Delete device | 200, 400 no JWT, 401 invalid, 409 user/device missing, 500 failure |

---

## PhotosController — `photos` (1)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| POST | `photos/upload/recording-photo` | body `UploadRecordingPhotoRequest` | JWT | Upload photo attached to a recording | 200, 400 no JWT, 401 invalid, 409 failure |

---

## RecordingsController — `recordings` (12)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| GET | `recordings` | `userId` query int? optional, `parts` query bool=false, `sound` query bool=false | none | List non-deleted recordings, optional owner filter | 200 array, 204 none, 500 failure |
| GET | `recordings/deleted` | — | JWT + admin | List soft-deleted recordings | 200 array, 204 none, 400 no JWT, 401 not admin, 500 failure |
| GET | `recordings/{id:int}` | `id` route int, `parts` query bool=false, `sound` query bool=false | none | Get single recording | 200, 204 not found/deleted |
| GET | `recordings/part/{recId:int}/{partId:int}/sound` **[Obsolete → use `part/{partId:int}/sound`]** | `recId` route int, `partId` route int | none | Download part audio (legacy dual-id form) | 200 wav file, 404 not found |
| GET | `recordings/part/{partId:int}/sound` | `partId` route int | none | Download part audio | 200 wav file, 404 not found |
| DELETE | `recordings/{id:int}` | `id` route int, `final` query bool=false | JWT + (owner or admin); `final=true` requires admin | Delete recording (soft or permanent) | 200, 400 no JWT, 401 invalid/lacks permission, 404 not found, 409 failure |
| POST | `recordings` | body `RecordingUploadRequest` | JWT | Upload new recording, schedules follow-up check job | 200 id, 400 no JWT, 401 invalid/user missing, 409 failure |
| POST | `recordings/part` | body `RecordingPartUploadRequest` | JWT | Upload recording part (JSON body incl. audio data) | 200 id, 400 no JWT, 401 invalid, 500 failure |
| POST | `recordings/part-new` | form `RecordingPartUploadRequest` + `file` (audio) | JWT | Upload recording part as multipart + enqueue for dialect classification | 200 id, 400 no JWT, 401 invalid, 500 failure |
| GET | `recordings/incomplete` | — | JWT | List caller's incomplete recordings | 200 array, 204 none, 400 no JWT, 401 invalid/user missing, 500 failure |
| PATCH | `recordings/{id:int}` | `id` route int, body `UpdateRecordingRequest` | JWT + (owner or admin) | Update recording | 200, 400 no JWT, 401 invalid/lacks permission, 404 not found, 409 failure |
| GET | `recordings/dialects` | — | none | List known bird dialects | 200 array |

---

## FilteredRecordingsController — `/recordings/filtered` (13)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| GET | `recordings/filtered` | `recordingId` query int? optional, `verified` query bool=false | none | List filtered parts, optional recording/verified filter | 200 array, 204 none, 409 failure |
| GET | `recordings/filtered/{fpId:int}` | `fpId` route int | none | Get single filtered part | 200, 409 not found |
| POST | `recordings/filtered` | body `FilteredRecordingPartUploadRequest` (implicit `[FromBody]`) | JWT | Upload filtered part | 200, 400 no JWT, 401 invalid, 409 failure |
| POST | `recordings/filtered/post-confirmed-dialect` | body `PostConfirmedDialectRequest` | JWT + admin | Create manually-confirmed dialect detection + filtered part | 200, 400 no JWT/bad dialect code, 401 not admin, 409 recording missing, 500 failure |
| PATCH | `recordings/filtered/update-confirmed-dialect` | body `UpdateConfirmedDialectRequest` | JWT + admin | Update confirmed dialect/time range for a filtered part | 200, 400 no JWT/bad dialect code, 401 not admin, 409 part missing, 500 failure |
| PATCH | `recordings/filtered/{fpId:int}` | `fpId` route int, body `FilteredRecordingPartUpdateRequest` | JWT + admin | Update filtered part | 200, 400 no JWT, 401 not admin, 409 not found, 500 failure |
| DELETE | `recordings/filtered/delete-confirmed-dialect/{filteredPartId:int}` **[Obsolete → use `{fpId}` DELETE]** | `filteredPartId` route int | JWT + admin | Delete filtered part's confirmed dialect entry (delegates to DeleteFilteredPartAsync) | 200, 400 no JWT, 401 not admin, 409 not found |
| DELETE | `recordings/filtered/{fpId:int}` | `fpId` route int | JWT + admin | Delete filtered part | 200, 400 no JWT, 401 not admin, 409 not found |
| GET | `recordings/filtered/detected/` | — | none | List all detected dialect entries | 200 array, 409 failure |
| GET | `recordings/filtered/detected/{ddId:int}` | `ddId` route int | none | Get single detected dialect entry | 200, 409 not found |
| POST | `recordings/filtered/detected/` | body `DetectedDialectUploadRequest` | JWT + admin | Create detected dialect entry | 201, 400 no JWT, 401 not admin, 409 failure |
| PATCH | `recordings/filtered/detected/` | body `UpdateDetectedDialectRequest` | JWT + admin | Update detected dialect entry | 200, 400 no JWT, 401 not admin, 409 failure |
| DELETE | `recordings/filtered/detected/{ddId:int}` | `ddId` route int | JWT + admin | Delete detected dialect entry | 200, 400 no JWT, 401 not admin, 409 failure |

---

## UsersController — `users` (10)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| GET | `users` | — | JWT + admin | List all users | 200 array, 400 no JWT, 401 not admin, 500 failure |
| GET | `users/get-id` | — | JWT | Get caller's own user id | 200 id, 400 no JWT, 401 invalid/not found |
| GET | `users/{userId:int}` | `userId` route int | none required; JWT optionally reveals email if self/admin | Get user profile (email hidden unless self/admin) | 200, 409 not found |
| PATCH | `users/{userId:int}` | `userId` route int, body `UpdateUserModel` | JWT + (self or admin) | Update user profile | 200, 400 no JWT/lacks permission, 401 invalid, 409 failure |
| DELETE | `users/{userId:int}` | `userId` route int | JWT + (self or admin) | Delete user | 200, 400 no JWT, 401 invalid/lacks permission, 404 failure |
| GET | `users/{userId:int}/verify-email` | `userId` route int, `jwt` query string (verification token) | verification JWT | Verify email via link, redirects to result page | redirect |
| PATCH | `users/{userId:int}/change-password` | `userId` route int, body `ChangePasswordRequest` | JWT matching user | Change password | 200, 400 no JWT/bad email, 401 invalid/mismatch, 404 not found, 500 failure |
| GET | `users/exists` | `userId` query int? optional, `email` query string? optional (email takes precedence) | none | Check user existence by id or email | 409 exists, 200 not exists, 400 neither provided |
| POST | `users/{userId:int}/upload-profile-photo` | `userId` route int, body `UserProfilePhotoModel` | JWT | Upload profile photo | 200, 400 no JWT, 401 invalid, 409 failure |
| GET | `users/{userId:int}/get-profile-photo` | `userId` route int | none | Get profile photo | 200, 404 none |

---

## MapController — `map` (1)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| GET | `map/{*path}` | `path` route string (catch-all) + forwarded query string | none | Reverse proxy to Mapy.cz API with server API key attached | proxied status/body/content-type |

---

## UtilsController — `utils` (6)

| Method | Route | Params | Auth | Purpose | Responses |
|---|---|---|---|---|---|
| HEAD | `utils/health` | — | none | Health check | 200 |
| GET | `utils/fix-same-dates` | — | JWT + admin | Maintenance: fix recording parts with identical dates | 200, 400 no JWT, 401 not admin |
| GET | `utils/normalize-existing-audios` | — | JWT + admin | Maintenance: normalize volume of existing recording audio | 200, 400 no JWT, 401 not admin |
| GET | `utils/analyze-parts` | — | JWT + admin | Maintenance: re-analyze existing recording parts | 200, 400 no JWT, 401 not admin |
| POST | `utils/send-notification` | body `SendNotificationRequest` | JWT + admin | Send custom push notification to a user's devices | 200 (even on per-device send errors), 400 no JWT, 401 not admin, 409 devices missing |
| GET | `utils/classify` | — | JWT + admin | Enqueue recordings prepared for classification through the dialect model | 200 array enqueued, 400 no JWT, 401 not admin, 409 load failure |

---

## Not covered

- `DictionaryController` (`Dictionary/DictionaryController.cs`) — empty stub class, no routes/endpoints defined.
