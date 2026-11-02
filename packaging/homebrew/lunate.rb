class Lunate < Formula
  desc "Native C# coding agent for the terminal"
  homepage "https://github.com/wdaniel1993/lunate"
  license "MIT"

  on_macos do
    depends_on arch: :arm64
  end

  on_linux do
    depends_on arch: :x86_64
  end

  if OS.mac?
    url "https://github.com/wdaniel1993/lunate/releases/download/v0.2.1/lunate-osx-arm64.tar.gz"
    sha256 "SHA256_PLACEHOLDER_OSX_ARM64"
  elsif OS.linux?
    url "https://github.com/wdaniel1993/lunate/releases/download/v0.2.1/lunate-linux-x64.tar.gz"
    sha256 "SHA256_PLACEHOLDER_LINUX_X64"
  end

  def install
    bin.install "lunate"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/lunate --version")
  end
end
