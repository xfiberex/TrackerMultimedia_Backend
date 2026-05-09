# ── PASOS DE DEPLOY A RENDER ────────────────────────────────────────────────

## Backend (https://render.com)

1. **Crear credenciales OAuth de producción** (una única app de GitHub para prod):
   - Ir a: https://github.com/settings/developers → "New OAuth App"
   - Application name: `TrackerMultimedia` (producción)
   - Homepage URL: tu dominio final (ej: https://tracker-multimedia.com)
   - Authorization callback URL: https://tu-backend-render-url.onrender.com/api/auth/github/callback
   - Copiar el ClientId y ClientSecret

2. **Conectar repo a Render y crear servicio web**:
   - Ir a render.com → New → Web Service
   - Conectar repositorio: TrackerMultimedia
   - Name: `trackermultimedia-backend`
   - Root Directory: (dejar vacío, Render auto-detecta Backend/)
   - Runtime: Docker
   - Plan: Starter (o superior si es necesario)
   - Region: Virginia (o el más cercano)

3. **Configurar variables de entorno en Render** (Environment):
   ```
   ASPNETCORE_ENVIRONMENT = Production
   Database__ApplyMigrationsOnStartup = true
   
   # SMTP (si se habilita):
   Smtp__Host = sandbox.smtp.mailtrap.io
   Smtp__Port = 2525
   Smtp__Username = <mailtrap-username>
   Smtp__Password = <mailtrap-password>
   Smtp__FromAddress = noreply@tu-app.com
   Smtp__FromName = TrackerMultimedia
   Smtp__Enabled = true
   
   # Google OAuth (si se habilita):
   OAuth__Google__Enabled = true
   OAuth__Google__ClientId = <google-client-id-producción>
   OAuth__Google__ClientSecret = <google-client-secret-producción>
   OAuth__Google__RedirectUri = https://tu-backend-render-url.onrender.com/api/auth/google/callback
   
   # GitHub OAuth:
   OAuth__GitHub__Enabled = true
   OAuth__GitHub__ClientId = <github-client-id-producción>
   OAuth__GitHub__ClientSecret = <github-client-secret-producción>
   OAuth__GitHub__RedirectUri = https://tu-backend-render-url.onrender.com/api/auth/github/callback
   
   # Otros:
   Jwt__Secret = <generar cadena aleatoria de 32+ caracteres>
   Security__ApiKey = <generar cadena aleatoria>
   ConnectionStrings__DefaultConnection = <PostgreSQL connection string>
   App__FrontendBaseUrl = https://tu-frontend-netlify-url.netlify.app
   Cors__AllowedOrigins__0 = https://tu-frontend-netlify-url.netlify.app
   ```

4. **Deploy**:
   - Presionar "Deploy"
   - Monitorear logs en Render para verificar migraciones y arranque
   - La base de datos será creada/migrada automáticamente en el primer inicio

## Frontend (https://netlify.com)

1. **Conectar repo a Netlify**:
   - New site → Import an existing project
   - Seleccionar repositorio TrackerMultimedia
   - Build command: `cd Frontend && npm run build`
   - Publish directory: `Frontend/dist`

2. **Configurar variables de entorno en Netlify** (Build & deploy → Environment):
   ```
   VITE_API_BASE_URL = https://tu-backend-render-url.onrender.com
   ```

3. **Deploy**:
   - Presionar "Deploy site"
   - Esperar a que termine la compilación

## Post-Deploy

- Registrar las redirect URIs finales en Google Cloud Console
- Probar flujos de OAuth (Google, GitHub)
- Probar SMTP si está habilitado
- Configurar dominio personalizado en Render y Netlify si es necesario