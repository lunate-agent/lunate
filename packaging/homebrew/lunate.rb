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
    url "https://github.com/lunate-agent/lunate/releases/download/v0.8.0/lunate-osx-arm64.tar.gz"
    sha256 "2c35ca2a21f44020c4a79f6fa3bd0aed8d8185e45c4b55b27cdbe4a974cdf9fc"
  elsif OS.linux?
    url "https://github.com/lunate-agent/lunate/releases/download/v0.8.0/lunate-linux-x64.tar.gz"
    sha256 "226e5e52eef5ac64ef26a2fafc387b81676a1d8c8aefb8dee7c1401ecc7c3fd0"
  end

  def install
    bin.install "lunate"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/lunate --version")
  end
end
