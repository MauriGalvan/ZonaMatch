# Photon: search-as-you-type geocoder over OpenStreetMap data (embedded OpenSearch, needs Java 21+).
# This image only contains the Photon jar. It is used to BUILD the index (service photon-amba, README
# step 6.2). The image teammates and Azure run is built from the infrastructure repo
# (docker/Dockerfile.photon.example) and already includes the index.
#
# Photon 1.3.0 is the 1.x release in use. If the build fails with a 404, confirm the jar name on
# https://github.com/komoot/photon/releases and adjust the URL below.
FROM eclipse-temurin:21-jre

ARG PHOTON_VERSION=1.3.0
ADD https://github.com/komoot/photon/releases/download/${PHOTON_VERSION}/photon-${PHOTON_VERSION}.jar /photon/photon.jar

# OpenSearch refuses to run as root; the volume mounted at photon_data inherits this owner
RUN useradd --system --home-dir /photon photon \
    && mkdir -p /photon/photon_data \
    && chown -R photon:photon /photon

USER photon
WORKDIR /photon
EXPOSE 2322

ENTRYPOINT ["java", "-jar", "/photon/photon.jar"]
# -data-dir is the parent of the photon_data folder
CMD ["serve", "-listen-ip", "0.0.0.0", "-data-dir", "/photon"]
