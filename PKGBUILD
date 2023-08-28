
# Maintainer: Archeb <archebasic@hotmail.com>
pkgname=opentrace
pkgver=1.2.3.3
pkgrel=1
pkgdesc="OpenTrace is a cross-platform GUI wrapper for NextTrace. Bringing you the familiar traceroute experience."
arch=('x86_64')
url="https://github.com/Archeb/opentrace"
license=('GPL3')
depends=('nexttrace>=1.1.7') 
source=("${pkgname}-${pkgver}.tar.gz::https://github.com/Archeb/opentrace/releases/download/v${pkgver}/linux-x64.tar.gz"
        "opentrace.desktop::https://raw.githubusercontent.com/Archeb/opentrace/master/opentrace.desktop"
        "logo.png::https://raw.githubusercontent.com/nxtrace/Ntrace-core/main/asset/logo.png")
sha256sums=('ab87088ab4b506f248e8b3f640f47ae5a90538ba565374235b62b4c8ce50852d'
            '69f8c4799f6db03bf17cd78b1de7a18d939ec5e282190942172dbe13e39c2075'
            '93cf17802f2691d63e29a7020afb0c7c39782c85212ce4b795cc8486f36c758d')

package() {
  install -Dm755 -d "${pkgdir}/opt/${pkgname%-bin}"
  cp -r "${srcdir}/"* "${pkgdir}/opt/${pkgname%-bin}"
  mkdir -p ${pkgdir}/usr/bin
  ln -sf "${pkgdir}/opt/${pkgname%-bin}/OpenTrace" ${pkgdir}/usr/bin/${pkgname}
  install -Dm644 "../opentrace.desktop" "${pkgdir}/usr/share/applications/opentrace.desktop"
  install -Dm644 "../logo.png" "$pkgdir/usr/share/pixmaps/opentrace.png"
}