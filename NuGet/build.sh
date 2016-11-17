#!/bin/bash

BINDIR=/Library/Frameworks/Mono.framework/Commands
XBUILD=${BINDIR}/xbuild
NUGET=${BINDIR}/nuget
VERSION_FILE=version.txt

# Fiddle with version info
VERSION=$(cat "${VERSION_FILE}")
VERSION_MAJOR=$(cat "${VERSION_FILE}" | cut -d "." -f 1)
VERSION_MINOR=$(cat "${VERSION_FILE}" | cut -d "." -f 2)
VERSION_BUILD=$(cat "${VERSION_FILE}" | cut -d "." -f 3)

if [ -z "${VERSION_MAJOR}" ]; then
	echo "No version number, creating empty version file, please run again"
	echo "0.1" > "${VERSION_FILE}"
	exit 1
fi

if [ "${VERSION_MAJOR}" -eq "0" ] && [ "${VERSION_MINOR}" -eq "0" ]; then
	echo "Looks like an empty version?"
	exit 1
fi

if [ -z "${VERSION_BUILD}" ]; then
	VERSION_MINOR=$((VERSION_MINOR+1))
	VERSION="${VERSION_MAJOR}.${VERSION_MINOR}"
else
	VERSION_BUILD=$((VERSION_BUILD+1))
	VERSION="${VERSION_MAJOR}.${VERSION_MINOR}.${VERSION_BUILD}"
fi

echo "Building v${VERSION}"

# Update AssemblyInfo.cs to get the correct version in the assemblies
SED_EXPR="s/.*\[assembly\: AssemblyVersion.*/[assembly: AssemblyVersion\(\"${VERSION}\"\)]/"

for CFGFILE in $(find .. -type f -name AssemblyInfo.cs); do
	NEWCFG=$(sed "${SED_EXPR}" "${CFGFILE}")

	echo "${NEWCFG}" > "${CFGFILE}"
done

# Update *.csproj to get the correct version
SED_EXPR="s/.*\<ReleaseVersion\>.*/    \<ReleaseVersion\>${VERSION}\<\/ReleaseVersion\>/"
for CSPROJ in $(find .. -type f -name "*.csproj"); do
	NEWCFG=$(sed "${SED_EXPR}" "${CSPROJ}")

	echo "${NEWCFG}" > "${CSPROJ}"
done

# Update *.sln to get the correct version
SED_EXPR="s/.*\version = .*/		version = ${VERSION}/"
for SLN in $(find .. -type f -name "*.sln"); do
	NEWCFG=$(sed "${SED_EXPR}" "${SLN}")

	echo "${NEWCFG}" > "${SLN}"
done


"${XBUILD}" "/p:Configuration=Release" "/target:Clean" "../Ceen.sln"
"${XBUILD}" "/p:Configuration=Release" "../Ceen.sln"

find . -type f -name "*.exe" | xargs rm
find . -type f -name "*.dll" | xargs rm
cp ../Ceen.Httpd.Cli/bin/Release/*.dll .
cp ../Ceen.Httpd.Cli/bin/Release/*.exe .

SED_EXPR="s/.*\<version\>.*/    \<version\>${VERSION}\<\/version\>/"
SED_EXPR2="s/.*\<dependency.*id\=\"Ceen.Httpd\".*/        \<dependency id\=\"Ceen.Httpd\" version\=\"${VERSION}\" \/\>/"
for NUSPEC in $(find . -type f -name "*.nuspec"); do
	sed "${SED_EXPR}" "${NUSPEC}" > "${NUSPEC}.tmp"
	sed "${SED_EXPR2}" "${NUSPEC}.tmp" > "${NUSPEC}.tmp2.nuspec"
	"${NUGET}" "pack" "${NUSPEC}.tmp2.nuspec"
	rm "${NUSPEC}.tmp" "${NUSPEC}.tmp2.nuspec"
done

rm *.exe *.dll

# Write updated version info back
echo "${VERSION}" > "${VERSION_FILE}"

for NUSPEC in $(find . -type f -name "*.nuspec"); do
	FILENAME=$(basename "${NUSPEC}")
	FILENAME="${FILENAME%.*}"

	echo "${NUGET} push ${FILENAME}.${VERSION}.nupkg"
done