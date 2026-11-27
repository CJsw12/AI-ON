plugins { id("com.android.application") }
android {
    namespace = "kr.aion.heartstream"
    compileSdk = 36
    defaultConfig { applicationId = "kr.aion.heartstream"; minSdk = 30; targetSdk = 36; versionCode = 1; versionName = "1.0" }
}
dependencies {
    implementation("androidx.health:health-services-client:1.1.0")
    implementation("com.google.guava:guava:33.4.8-android")
    testImplementation("junit:junit:4.13.2")
}
