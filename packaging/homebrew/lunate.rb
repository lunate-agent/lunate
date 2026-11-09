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
    url "https://github.com/wdaniel1993/lunate/releases/download/v0.3.0/lunate-osx-arm64.tar.gz"
    sha256 "1a0d729270dd9ed45e917b6a9f9d1dd3a58f5a27c718649524e143e213f04aaf"
  elsif OS.linux?
    url "https://github.com/wdaniel1993/lunate/releases/download/v0.3.0/lunate-linux-x64.tar.gz"
    sha256 "9c53bb56239a6a1825e0c2f784f824abfdc2851784d044cfb7a2e244f95655be"
  end

  def install
    bin.install "lunate"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/lunate --version")
  end
end
