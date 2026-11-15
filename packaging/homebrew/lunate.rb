class Lunate < Formula
  desc "Native C# coding agent for the terminal"
  homepage "https://github.com/lunate-agent/lunate"
  license "MIT"

  on_macos do
    depends_on arch: :arm64
  end

  on_linux do
    depends_on arch: :x86_64
  end

  if OS.mac?
    url "https://github.com/lunate-agent/lunate/releases/download/v0.4.0/lunate-osx-arm64.tar.gz"
    sha256 "ca69fa98ef981810b4565b2ac7c64c22377915b53ba9cfaa1fdb5a2ebc77ca49"
  elsif OS.linux?
    url "https://github.com/lunate-agent/lunate/releases/download/v0.4.0/lunate-linux-x64.tar.gz"
    sha256 "d0ad6cf1fc2d324a21fa2c3f4979dbb0c6fac9ec2be714a2b998c5132fd7feed"
  end

  def install
    bin.install "lunate"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/lunate --version")
  end
end
