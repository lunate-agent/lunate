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
    sha256 "0e69e74129eca1f7a180d5d490b3916ebffc1d9a3393a90d09118bf41a9fec8e"
  elsif OS.linux?
    url "https://github.com/wdaniel1993/lunate/releases/download/v0.2.1/lunate-linux-x64.tar.gz"
    sha256 "397d2ccab334282ba3f75b09670c7892a7a9113e124ca4b2bb63b3f4793fe8fc"
  end

  def install
    bin.install "lunate"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/lunate --version")
  end
end
