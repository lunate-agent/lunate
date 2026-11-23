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
    url "https://github.com/lunate-agent/lunate/releases/download/v0.7.0/lunate-osx-arm64.tar.gz"
    sha256 "531eda93b8d9b929471e9e162808662d295082ee99e8ac79866750cef1146ca9"
  elsif OS.linux?
    url "https://github.com/lunate-agent/lunate/releases/download/v0.7.0/lunate-linux-x64.tar.gz"
    sha256 "516c581c422a7b2c9bbf734d7e2fc73559f70b45ced642a7ce909c672a83dfd4"
  end

  def install
    bin.install "lunate"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/lunate --version")
  end
end
