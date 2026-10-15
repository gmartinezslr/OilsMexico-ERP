Múltiples **correos, teléfonos y direcciones para clientes y proveedores**


Actúa como un experto en gestión de bases de datos y operaciones comerciales. Necesito que crees una plantilla o estructura de "Handoff" (traspaso de información) optimizada. Su objetivo principal es reestructurar y enriquecer los datos de nuestros Clientes y Proveedores para permitir múltiples puntos de contacto y asegurar la continuidad operativa.


La estructura debe cumplir estrictamente con las siguientes reglas de negocio:


1. Campos unificados para Clientes y Proveedores:

   - Debe permitir múltiples Correos Electrónicos (ej. Principal, Compras/Ventas, Facturación).

   - Debe permitir múltiples Teléfonos (ej. Celular, Oficina, WhatsApp).

   - Debe permitir múltiples Direcciones (ej. Fiscal, Entrega, Sucursal).


2. Reglas específicas para CLIENTES:

   - Además del contacto principal, se debe incluir obligatoriamente una sección para "Persona de Respaldo / Quien Recibe". Esto servirá para saber con quién comunicarse o quién puede recibir un pedido/notificación si el contacto principal no está presente.


3. Reglas específicas para PROVEEDORES (Empresas):

   - Cada contacto listado dentro de la empresa proveedora debe incluir obligatoriamente su "Puesto / Cargo" (ej. Ejecutivo de Cuenta, Gerente de Logística, Encargado de Finanzas) para saber exactamente a quién recurrir según la necesidad.


Por favor, genera:

1. La estructura limpia en formato Markdown o Tabla para usarla de plantilla.

2. Un ejemplo ficticio de cómo se vería un Handoff completado para un Cliente.

3. Un ejemplo ficticio de cómo se vería un Handoff completado para un Proveedor.




### lee el archivo: "Plantilla de Handoff — Clientes y Proveedores.md" revisa si cumple o se puede mejorar la propuesta





La propuesta original **cubría las reglas principales**, pero necesitaba ajustes para servir como plantilla operativa: el contenido estaba escapado y dentro de bloques de código, duplicaba datos de contacto y no aclaraba suficientemente cómo validar a la persona de respaldo ni cómo seleccionar direcciones predeterminadas.

Revisé y actualicé `Plantilla de Handoff — Clientes y Proveedores.md`. Ahora incluye:

- Una plantilla en Markdown legible con campos repetibles para correos, teléfonos y direcciones. 

- IDs de contacto para asociar los medios sin duplicar información. 

- La sección obligatoria para clientes: respaldo distinto del contacto principal, con medio de contacto y alcance de autorización. 

- La regla de puesto/cargo obligatorio para cada contacto de proveedor. 

- Direcciones predeterminadas según su uso, y datos comerciales separados para clientes y proveedores. 

- Un ejemplo ficticio completo para cada tipo. También evité exigir que siempre exista correo o teléfono principal; se registra lo disponible y se marca lo pendiente. 

Verifiqué que las tablas del documento mantengan una estructura consistente. **Conclusión:** la versión revisada cubre los requisitos solicitados y agrega criterios útiles para reducir ambigüedades en la captura.




Voy a revisar las reglas del handoff y cómo están modelados/gestionados hoy Clientes y Proveedores para traducirlas a cambios reales de aplicación y base de datos, no solo a documentación. Primero confirmaré el alcance existente y las modificaciones locales para no pisar trabajo ajeno.

Ran terminal command: git status --short

Find files matching `\*\*/\{Cliente,Proveedor\}\*.cs`

Find files matching `\*\*/\*Cliente\*.razor`

Find files matching `\*\*/\*Proveedor\*.razor`

Find files matching `\*\*/\*DbContext\*.cs`

Read , lines 1 to 95 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\Plantilla de Handoff — Clientes y Proveedores.md", "view\_range": \[ 1, 95 \] \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Domain\\Entities\\Cliente.cs" \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Domain\\Entities\\Proveedor.cs" \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Application\\Interfaces\\ClientesProveedores.cs" \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Services\\ClientesService.cs" \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Services\\ProveedoresService.cs" \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Persistence\\ErpDbContext.cs" \}

Find files matching `src/OilsMexico.Infrastructure/Migrations/\*.cs`

Read , lines 1 to 240 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Pages\\ERP\\Clientes.razor", "view\_range": \[ 1, 240 \] \}

Read , lines 1 to 240 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Pages\\ERP\\Proveedores.razor", "view\_range": \[ 1, 240 \] \}

Find files matching `\*\*/\*CatalogoDto.cs`

Search for `record ClienteCatalogoDto|class ClienteCatalogoDto|record ProveedorCatalogoDt...`

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Pages\\ERP\\Clientes.razor.cs" \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Pages\\ERP\\Proveedores.razor.cs" \}

Find files matching `\*\*/\*.csproj`

Find files matching `\*\*/\*Test\*.cs`

Read , lines 110 to 190 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Application\\DTOs\\CatalogosDtos.cs", "view\_range": \[ 110, 190 \] \}

Search for `CreateTable\\(\\s\*name: "(clientes|proveedores)"|ToTable\\("(clientes|proveedores)"`

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj" \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\OilsMexico.Web.csproj" \}

Find files matching `\*\*/Directory.\*`

Search for `UseNpgsql|UseSqlite|UseSqlServer|MigrationsAssembly|AddDbContext\<ErpDbContext\>`

Read , lines 90 to 175 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Pages\\ERP\\Clientes.razor", "view\_range": \[ 90, 175 \] \}

Read , lines 90 to 175 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Pages\\ERP\\Proveedores.razor", "view\_range": \[ 90, 175 \] \}

Search for `IEntityTypeConfiguration\<Cliente\>|IEntityTypeConfiguration\<Proveedor\>|ToTable...`

Search for `AddScoped\<IClientesService|AddScoped\<IProveedoresService`

Ran terminal command: dotnet --version; dotnet ef --version

Read , lines 55 to 100 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Persistence\\Configs\\VentaConfigs.cs", "view\_range": \[ 55, 100 \] \}

Read , lines 35 to 80 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Persistence\\Configs\\SepomexConfigs.cs", "view\_range": \[ 35, 80 \] \}

Read , lines 170 to 255 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Migrations\\ErpDbContextModelSnapshot.cs", "view\_range": \[ 170, 255 \] \}

Search for `UseSnakeCaseNamingConvention|NpgsqlSnakeCaseNameTranslator|snake\_case`

El handoff define cambios reales al padrón: los formularios actuales solo guardan un correo, un teléfono y una dirección por tercero. Implementaré colecciones relacionales para contactos y medios, migraré los datos existentes sin borrar los campos legacy y ampliaré los formularios de Clientes/Proveedores con validaciones para respaldo y cargo.

Edit , , , , ,

Edit

Edit ,

Edit

Edit

Edit

Ran terminal command: dotnet build src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --no-restore

Search for `@code|CodigoPostal`

Read , lines 100 to 155 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Shared\\DireccionSepomex.razor", "view\_range": \[ 100, 155 \] \}

Read , lines 1 to 15 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Shared\\DireccionSepomex.razor", "view\_range": \[ 1, 15 \] \}

Search for `\\\[Parameter\\\]|public string`

Find files matching `\*\*/DireccionSepomex.razor.cs`

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Shared\\DireccionSepomex.razor.cs" \}

Search for `\\\[Parameter\\\]|CodigoPostal|Colonia`

Edit

Edit

Edit

Edit

Edit

Edit

Ran terminal command: dotnet build src\\OilsMexico.Web\\OilsMexico.Web.csproj --no-restore

Los formularios ya compilan con un cuarto paso para contactos y direcciones múltiples. Ahora voy a generar la migración EF Core y asegurar que los correos, teléfonos y domicilios legacy se copien a las nuevas tablas para que los datos actuales sigan disponibles.

Ran terminal command: dotnet ef migrations add TercerosContactos --project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --startup-project src\\OilsMexico.Web\\OilsMexico.Web.csproj --context ErpDbContext --output-dir Migrations Tool execution failed with exit code 1

Ran terminal command: dotnet ef migrations add TercerosContactos --project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --startup-project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --context ErpDbContext --output-dir Migrations

Find files matching `src/OilsMexico.Infrastructure/Migrations/\*TercerosContactos\*`

Read , lines 1 to 12 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Migrations", "view\_range": \[ 1, 12 \] \}

Search for `terceros\_contactos|terceros\_correos|terceros\_telefonos|terceros\_direcciones`

Read , lines 1 to 220 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Migrations\\20261015205530\_TercerosContactos.cs", "view\_range": \[ 1, 220 \] \}

Search for `CreateTable\\(|AddColumn|AlterColumn|DropColumn`

Search for `cuentas\_bancarias|CreateTable\\(\\s\*name: "gastos"|CreateTable\\(\\s\*name: "perio...`

Ran terminal command: git status --short; git diff -- src\\OilsMexico.Infrastructure\\Migrations\\ErpDbContextModelSnapshot.cs

Ran terminal command: $s = git show HEAD:src/OilsMexico.Infrastructure/Migrations/ErpDbContextModelSnapshot.cs; \[pscustomobject\]@\{CommittedSnapshotHasBank=($s -match 'cuentas\_bancarias'); CommittedSnapshotHasGastos=($s -match 'ToTable("gastos"'); CommittedSnapshotHasPeriods=($s -match 'periodos\_contables'); CommittedSnapshotHasDiasCreditoProveedor=($s -match 'dias\_credito')\} | Format-List; git show --stat --oneline HEAD -- src/OilsMexico.Infrastructure/Migrations

Read , lines 360 to 510 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Migrations\\20261015205530\_TercerosContactos.cs", "view\_range": \[ 360, 510 \] \}

Search for `uq\_contactos\_cliente|ix\_clientes.\*principal`

Edit , ,

Edit ,

Ran terminal command: dotnet ef migrations remove --project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --startup-project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --context ErpDbContext --force

Ran terminal command: dotnet ef migrations add TercerosContactos --project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --startup-project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --context ErpDbContext --output-dir Migrations

"Search" failed Tool execution failed: Search paths do not exist: D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Migrations\\202610152.\*\_TercerosContactos.cs

Find files matching `src/OilsMexico.Infrastructure/Migrations/\*TercerosContactos.cs`

Read , lines 430 to 500 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Migrations\\20261015205740\_TercerosContactos.cs", "view\_range": \[ 430, 500 \] \}

Edit

Search for `uq\_contactos\_cliente\_principal|uq\_contactos\_proveedor\_principal|uq\_contactos\_...`

Edit , ,

Ran terminal command: dotnet build src\\OilsMexico.Web\\OilsMexico.Web.csproj --no-restore

Search for `error CS|Compilación correcta|Error\\(es\\)|Advertencia\\(s\\)`

Ran terminal command: $out = Join-Path $env:TEMP 'OilsMexico-TercerosContactos.sql'; dotnet ef migrations script 20261012235338\_CotizacionesOrdenesVenta 20261015205740\_TercerosContactos --project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --startup-project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --context ErpDbContext --output $out; if ($LASTEXITCODE -eq 0) \{ Select-String -Path $out -Pattern 'CREATE TABLE terceros\_|INSERT INTO terceros\_|uq\_contactos\_cliente\_principal|uq\_direcciones\_cliente\_predeterminada\_tipo' | ForEach-Object \{ $\_.Line \} \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\tests\\OilsMexico.IntegrationTests\\OilsMexico.IntegrationTests.csproj" \}

Read , lines 1 to 48 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Migrations\\20261015205740\_TercerosContactos.cs", "view\_range": \[ 1, 48 \] \}

Read , lines 1 to 205 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Shared\\TerceroContactosEditor.razor", "view\_range": \[ 1, 205 \] \}

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Web\\Components\\Shared\\TerceroDireccionesEditor.razor" \}

Read skill `run-tests`

Edit , ,

Edit ,

Find files matching `\*\*/\{global.json,Directory.Build.props,Directory.Packages.props\}`

Read , lines 1 to 120 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\tests\\OilsMexico.IntegrationTests\\PostgresP1Tests.cs", "view\_range": \[ 1, 120 \] \}

Search for `\<TestingPlatformDotnetTestSupport\>|xunit.v3|MSTest|NUnit|Microsoft.NET.Test.S...`

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\global.json" \}

Find files matching `\*\*/PgFixture.cs`

Read Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\tests\\OilsMexico.IntegrationTests\\PgFixture.cs" \}

Edit

Ran terminal command: dotnet test tests\\OilsMexico.IntegrationTests\\OilsMexico.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~TerceroContactosServiceTests Tool execution failed with exit code 1

Search for `error|Failed|Error Message|Passed!|Failed!|Exception|Result|No test|Total tests`

Read tool output Completed with input: \{ "path": "C:\\Users\\usuario\\AppData\\Local\\Temp\\1792098081535-copilot-tool-output-25432-7fbb95d4-e0b6-40d9-a5a9-08bc643d8868.txt", "view\_range": \[ 32, 53 \] \}

Edit

Edit

Edit ,

Ran terminal command: dotnet test tests\\OilsMexico.IntegrationTests\\OilsMexico.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~TerceroContactosServiceTests

Search for `EnsureCreatedAsync|MigrateAsync|SchemaPatch.AplicarAsync|PeriodoContablePatch...`

Search for `EnsureCreatedAsync|MigrateAsync|SchemaPatch.AplicarAsync|PeriodoContablePatch...`

Read , lines 1 to 45 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Persistence\\SeedData.cs", "view\_range": \[ 1, 45 \] \}

Read , lines 1 to 80 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Persistence\\SchemaPatch.cs", "view\_range": \[ 1, 80 \] \}

Read , lines 250 to 430 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Persistence\\SchemaPatch.cs", "view\_range": \[ 250, 430 \] \}

Read , lines 1 to 120 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Persistence\\BancosPatch.cs", "view\_range": \[ 1, 120 \] \}

Read , lines 1 to 100 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Persistence\\GastosPatch.cs", "view\_range": \[ 1, 100 \] \}

Encontré un detalle decisivo del proyecto: el arranque no ejecuta migraciones EF; actualiza bases existentes mediante `SchemaPatch`. Para que funcione tanto en instalaciones nuevas como en bases ya creadas, cambiaré la migración generada por el mecanismo idempotente propio del ERP, incluyendo el traspaso de los datos legacy.

Ran terminal command: dotnet ef migrations remove --project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --startup-project src\\OilsMexico.Infrastructure\\OilsMexico.Infrastructure.csproj --context ErpDbContext --force

"Search" failed Tool execution failed: Search paths do not exist: D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Migrations\\20261015205740\_TercerosContactos.cs

Edit ,

Edit

Edit

Edit

El parche de esquema ya está conectado al arranque idempotente del ERP, y añadí pruebas para verificar tanto la validación funcional como el traslado de datos legacy. Validaré el SQL con PostgreSQL real y volveré a compilar las pantallas.

Ran terminal command: dotnet test tests\\OilsMexico.IntegrationTests\\OilsMexico.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~TerceroContactosPatchTests

Search for `Correctas!|Con error!|Docker|Error|Total:`

Edit , ,

Edit ,

Ran terminal command: dotnet build src\\OilsMexico.Web\\OilsMexico.Web.csproj --no-restore

Search for `error CS|Compilación correcta|Error\\(es\\)|Advertencia\\(s\\)`

Ran terminal command: dotnet test tests\\OilsMexico.IntegrationTests\\OilsMexico.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~TerceroContactos

Las cuatro pruebas enfocadas pasan, incluida una contra PostgreSQL que ejercita el parche de esquema y la migración idempotente de datos legacy. Haré una última revisión del diff para detectar inconsistencias y confirmar que no quedó una migración EF accidental en un proyecto que usa `SchemaPatch`.

Ran terminal command: git status --short; git diff --check; git diff --stat; Get-ChildItem -LiteralPath 'src\\OilsMexico.Infrastructure\\Migrations' -Filter '*TercerosContactos*'

Read , lines 1 to 190 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Services\\ClientesService.cs", "view\_range": \[ 1, 190 \] \}

Read , lines 1 to 180 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Services\\ProveedoresService.cs", "view\_range": \[ 1, 180 \] \}

Read , lines 1 to 230 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Services\\TerceroContactosMapping.cs", "view\_range": \[ 1, 230 \] \}

Read , lines 1 to 230 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\src\\OilsMexico.Infrastructure\\Persistence\\TerceroContactosPatch.cs", "view\_range": \[ 1, 230 \] \}

Ran terminal command: git diff -- src\\OilsMexico.Infrastructure\\Migrations\\ErpDbContextModelSnapshot.cs

Edit

Edit ,

Edit

Edit

Ran terminal command: dotnet test tests\\OilsMexico.IntegrationTests\\OilsMexico.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~TerceroContactos Tool execution failed with exit code 1

Search for `FAIL|Con error|Mensaje de error|Exception|error CS|Total:`

Read , lines 1 to 160 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\tests\\OilsMexico.IntegrationTests\\TerceroContactosPatchTests.cs", "view\_range": \[ 1, 160 \] \}

Edit

Read , lines 58 to 130 Completed with input: \{ "path": "D:\\GMS\\Jerry\\OilsMexico\\tests\\OilsMexico.IntegrationTests\\TerceroContactosPatchTests.cs", "view\_range": \[ 58, 130 \] \}

Edit

Ran terminal command: dotnet test tests\\OilsMexico.IntegrationTests\\OilsMexico.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~TerceroContactos

Ran terminal command: dotnet build src\\OilsMexico.Web\\OilsMexico.Web.csproj --no-restore

Ran terminal command: git status --short; git diff --check; git diff -- src\\OilsMexico.Infrastructure\\Migrations\\ErpDbContextModelSnapshot.cs; Get-ChildItem -LiteralPath 'src\\OilsMexico.Infrastructure\\Migrations' -Filter '*TercerosContactos*' | Select-Object -ExpandProperty Name

Search for `Alta de .\* en 3 pasos|paso de 3|paso \< 3|Math.Min\\(3`

Search for `TerceroContactosPatch.AplicarAsync`

Implementé en el ERP los requisitos principales del handoff:

- Clientes y proveedores ahora admiten **varios correos, teléfonos y direcciones**, con uso, preferencia principal y asociación opcional a una persona. Los buzones compartidos pueden quedar sin contacto individual.

- Clientes requieren una **persona de respaldo distinta del contacto principal**, con un medio de contacto y permiso para recibir pedidos o notificaciones.

- En proveedores, cada contacto registrado requiere **puesto o cargo**.

- El alta y edición ahora incluyen pasos específicos para direcciones y contactos en `Clientes.razor` y `Proveedores.razor`.

- Los datos se guardan en tablas relacionadas. El `parche de esquema`, conectado al arranque en `SchemaPatch.cs`, crea las tablas de forma idempotente y copia los correos, teléfonos y direcciones existentes, conservando compatibilidad con los campos anteriores.

**Verificación:** compilación web correcta; pasaron las 5 pruebas enfocadas, incluidas pruebas contra PostgreSQL para el parche, la migración de datos legacy y la edición de contactos. El build reportó una advertencia preexistente en `Metas.razor.cs`; no hubo errores.

