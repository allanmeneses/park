import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

val localProperties = Properties().apply {
    val f = rootProject.file("local.properties")
    if (f.exists()) {
        f.inputStream().use { stream -> load(stream) }
    }
}
/** Emulador / dev local — ver local.properties.example */
val parkingDebugApiBase =
    localProperties.getProperty("parking.api.base")?.trim()?.takeIf { s -> s.isNotEmpty() }
        ?: "http://10.0.2.2:8080/api/v1"

/**
 * Produção: mesma URL que o front (GitHub variable VITE_API_BASE), ex. Container Apps + /api/v1.
 * Ordem de precedência: `-Pparking.api.production=...` > local.properties > env PARKING_API_PRODUCTION > default.
 */
val parkingProductionApiBase =
    (project.findProperty("parking.api.production") as String?)?.trim()?.takeIf { it.isNotEmpty() }
        ?: localProperties.getProperty("parking.api.production")?.trim()?.takeIf { s -> s.isNotEmpty() }
        ?: System.getenv("PARKING_API_PRODUCTION")?.trim()?.takeIf { s -> s.isNotEmpty() }
        ?: "https://api.example.com/api/v1"

android {
    namespace = "com.estacionamento.parking"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.estacionamento.parking"
        minSdk = 26
        targetSdk = 34
        versionCode = 1
        versionName = "1.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        vectorDrawables { useSupportLibrary = true }
    }

    buildTypes {
        debug {
            buildConfigField("String", "API_BASE", "\"$parkingDebugApiBase\"")
        }
        release {
            isMinifyEnabled = false
            // Chave debug: APK instalável por USB para testes contra produção (não usar na Play Store).
            signingConfig = signingConfigs.getByName("debug")
            buildConfigField("String", "API_BASE", "\"$parkingProductionApiBase\"")
        }
    }
    buildFeatures {
        compose = true
        buildConfig = true
    }
    composeOptions {
        kotlinCompilerExtensionVersion = "1.5.14"
    }
    packaging {
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
        }
    }
    kotlinOptions {
        jvmTarget = "17"
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2024.05.00")
    implementation(composeBom)
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.material:material")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.activity:activity-compose:1.9.0")
    implementation("androidx.navigation:navigation-compose:2.7.7")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.0")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.0")
    implementation("com.squareup.retrofit2:retrofit:2.9.0")
    implementation("com.squareup.retrofit2:converter-moshi:2.9.0")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("com.squareup.moshi:moshi-kotlin:1.15.1")
    implementation("androidx.security:security-crypto:1.1.0-alpha06")
    implementation("com.google.zxing:core:3.5.3")
    implementation("com.journeyapps:zxing-android-embedded:4.3.0")
    debugImplementation("androidx.compose.ui:ui-tooling")
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:1.8.0")
    androidTestImplementation(composeBom)
    androidTestImplementation("androidx.compose.ui:ui-test-junit4")
    androidTestImplementation("androidx.test.ext:junit:1.1.5")
    androidTestImplementation("androidx.test.espresso:espresso-core:3.5.1")
}
