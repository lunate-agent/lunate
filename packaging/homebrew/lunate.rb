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
    url "https://github.com/wdaniel1993/lunate/releases/download/v0.2.0/lunate-osx-arm64.tar.gz"
    sha256 "dbc107d3606edc735a07fdbd5ad0275901d50b02943f742267914da3c48cfa79"
  elsif OS.linux?
    url "https://github.com/wdaniel1993/lunate/releases/download/v0.2.0/lunate-linux-x64.tar.gz"
    sha256 "eb6bc0e21eb7d97f256235bf5ec98978dbf8aa7402d6299007baa397af068e77"
  end

  def install
    bin.install "lunate"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/lunate --version")
  end
end
